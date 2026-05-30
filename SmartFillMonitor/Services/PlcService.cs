using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using SmartFillMonitor.Models;
using Modbus.Device;
using SmartFillMonitor.Services.Logs;
using System.Linq.Expressions;

namespace SmartFillMonitor.Services;
///<summery>
 ///PLC通信服务，用于Modbus rtu串口通信读取数据
 ///功能包含：
 ///-管理串口，连断开等操作
 ///-周期性轮询PLC数据并且通过事件公布
 ///提供一些命令接口给上层调用


    ///</summery>



    public class PlcService
    {
    //串口对象
    private static SerialPort? _serialPort;

    //NModbus4提供的Modbus master接口，用于读取/写入寄存器
    private static IModbusSerialMaster _modbusMaster;

    //取消令牌源，用于停止后台轮询任务
    private static CancellationTokenSource? _cts;

    //异步锁：确保同时只有一个读写操作在进行，防止串口冲突
    private static readonly SemaphoreSlim _ioLock = new SemaphoreSlim(1, 1);

    //Modbus 从站ID,默认为1
    private const byte SlaveID = 1;


    //当读取到的新的数据时候触发该事件，用于Ui层显示
    public static event EventHandler<DeviceStates>?DataReceived;


    //当连接状态发生变化时触发该事件(true连上 false断开)
    public static event EventHandler<bool> ConnectionChanged;

    //只读属性，表示当前是否已经连接上PLC
    public static bool IsConnected => _serialPort != null && _serialPort.IsOpen;

    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();



    public static async Task Initialize(DeviceSettings Settings)
    {
        //先断开旧的连接，如果有的，防止资源泄露或重复开闭串口

        await DisConnectAsync();
        if (Settings is { AutoConnect:true})
        {
            _serialPort = new SerialPort

            {
                PortName = Settings.PortName,
                BaudRate = Settings.BaudRate,
                DataBits = Settings.DataBit,
                Parity = ParseParity(Settings.Parity),//字符串转枚举
                StopBits = ParseStop(Settings.StopBit)
            };
            //尝试连接
            await ConnectAsync();
        }

        }


    private static Parity ParseParity(string s) => Enum.TryParse<Parity>(s, true, out var p)?p:Parity.None;
    private static StopBits ParseStop(string s) => Enum.TryParse<StopBits>(s, true, out var stop) ? stop : StopBits.One;
    public PlcService() 
    {
    
    
    
    
    }



    public static async Task ConnectAsync()
    {
        if (_serialPort == null) return;
        if (IsConnected) return;
        try
        {
            _serialPort.Open();
            _modbusMaster = ModbusSerialMaster.CreateRtu(_serialPort);
            _modbusMaster.Transport.ReadTimeout = 1000;//传输超时时间设置为1000ms
            _modbusMaster.Transport.WriteTimeout = 1000;

            LogService.Info($"PLC串口打开成功{_serialPort.PortName}");

             _cts= new CancellationTokenSource();
            _=Task.Run(() =>PollDataLoop(_cts.Token));
        }
        catch (Exception ex)
        {
            ConnectionChanged?.Invoke(null,false);
            LogService.Error($"连接串口失败{_serialPort?.PortName}", ex);
            
        }
        await Task.CompletedTask;//保持签名一致，此处没有其他异步操作，所以直接完成状态


    }


    //安全断开并且释放所有资源（取消后台轮询）
    public static async  Task DisConnectAsync()
    {
        //取消后台实时查询任务
        _cts?.Cancel();

        //等待异步锁，防止并发冲突
        await _ioLock.WaitAsync();

        try
        {
            if(_serialPort != null)
            {
                if (_serialPort.IsOpen) _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;

            }

            if (_modbusMaster != null)
            {

                _modbusMaster.Dispose();
                _modbusMaster = null;

            }
        }
        catch 
        {

            throw;
        }
        finally
        {
            //释放锁，允许其他操作继续
            _ioLock.Release();
            //通知订阅者已经断开
            ConnectionChanged?.Invoke(null, false);

        }
    }


    //后台轮询循环：持续读取PLC状态并且DataReceived事件公布数据
    private static async Task PollDataLoop(CancellationToken token)
    {
        int errCount = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!IsConnected)
                {
                    ConnectionChanged?.Invoke(null, false);
                    await Task.Delay(1000, token);
                    continue;

                }
                var state = await ReadStateAsync();
                if (state != null)
                {
                    errCount = 0;
                    ConnectionChanged?.Invoke(null, true);
                    DataReceived?.Invoke(null, state);
                }
                await Task.Delay(200, token);

            }
            catch (OperationCanceledException ex)
            {
                break;//收到取消请求，跳出循环

            }

            catch (Exception ex)
            {
                errCount++;
                if (errCount >= 3)
                {
                    LogService.Warn($"PLC通讯异常:{ex.Message}");
                    ConnectionChanged?.Invoke(null, false);
                    errCount = 0;


                }



                await Task.Delay(1000, token);//等待一段时间后重试

            }


        }
    }
    //从PLC读取当前设备状态并且封装为DeviceState对象返回
    public static async Task <DeviceStates> ReadStateAsync()
    {
       await _ioLock.WaitAsync();
        try
        {
            
            if (_modbusMaster == null) throw new InvalidOperationException("未连接");
           //1.读取数值区（假设读取10个寄存器）
            ushort[] registers = await _modbusMaster.ReadHoldingRegistersAsync(SlaveID, 0, 10);
            //2.读取条码区（假设从地址10开始读取，长度为10个寄存器）
            const ushort barcodeStart = 10;
            const ushort barcodeLength = 10;
            string barcode = string.Empty;

            try
            {
                ushort[] barcodeRes = await _modbusMaster.ReadHoldingRegistersAsync(SlaveID, barcodeStart,barcodeLength);
            barcode = ConverRegisterToString(barcodeRes);
            
            }
            catch (Exception ex)
            {

                LogService.Warn($"读取条码失败: {ex.Message}");
               
            }
            return new DeviceStates
            {
                //生产和时间类数据需要除以100得到实际值
                ActualCount = registers[ModbusConfigHelper.ActualCount],
                TargetCount = registers[ModbusConfigHelper.TargetCount],
                CurrentTemp = registers[ModbusConfigHelper.CurrentTemp]/100.0,
                SettingTemp = registers[ModbusConfigHelper.SettingTemp]/100.0,
                RunningTime = registers[ModbusConfigHelper.RunningTime]/100.0,
                CurrentCycleTime = registers[ModbusConfigHelper.CurrentCycleTime]/100.0,
                StandardCycleTime = registers[ModbusConfigHelper.StandardCycleTime]/100.0,
                LiquidLevel = registers[ModbusConfigHelper.LiquidLevel]/100.0,
                ValueOpen = registers[ModbusConfigHelper.ValueOpen] == 1,//数字1表示打开阀门，0表示关闭
                BarCode = barcode


            };

        }
        finally
        { _ioLock.Release(); }


    }
    private static string ConverRegisterToString(ushort[] regs)
    {
        //空检查
        if(regs== null || regs.Length == 0) return string.Empty;
        List<byte> bytes = new List<byte>();
        foreach (var reg in regs)
        {
            //如果寄存器为0，常用设备将0作为字符串结束，直接结束解析
            if (reg == 0) break;

            byte high = (byte)(reg >> 8);//high为寄存器的高8位（高字节）
            byte low = (byte)(reg &0xFF);//低8位
            if(high !=0) bytes.Add(high);
            if(low !=0) bytes.Add(low);
         }
        //按照ACSII解码字节序列化位字符串，并去掉两端留白
        return Encoding.ASCII.GetString(bytes.ToArray()).Trim();
    }

    public static async Task WriteCommandStateAsync(string command, bool value)
    {
        ushort address = command == "Start" ? (ushort)1 : (ushort)2;
        await _ioLock.WaitAsync();
        try
        {
            if (_modbusMaster == null) return;
            await _modbusMaster.WriteSingleRegisterAsync(SlaveID, address, (ushort)(value ? 1 : 0));
            LogService.Info($"写入指令:{command}={value}");
        }
        catch (Exception ex)
        {
            LogService.Error($"写入指令失败:{command}={value}", ex);
           
        }
        finally { _ioLock.Release(); }//最终确保释放IO锁

    }



}
