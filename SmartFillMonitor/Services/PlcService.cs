using Modbus.Device;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services.Logs;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
    private static TcpClient?   _tcpClient;
    private static IModbusMaster? _ipMaster;
    private static string? _lastTcpIp;
    private static int _lastTcpPort;

    //串口对象
    private static SerialPort? _serialPort;

    //NModbus4提供的Modbus master接口，用于读取/写入寄存器
    private static IModbusSerialMaster? _modbusMaster;

    //串口重连参数（效仿 TCP 模式，保存以便 PollDataLoop 自动重连）
    private static string? _lastPortName;
    private static int _lastBaudRate;
    private static int _lastDataBits;
    private static Parity _lastParity;
    private static StopBits _lastStopBits;

    //当前连接模式，用于 PollDataLoop 选择串口/TCP 重连策略
    private static ConnectionMode? _currentMode;

    //取消令牌源，用于停止后台轮询任务
    private static CancellationTokenSource? _cts;

    //异步锁：确保同时只有一个读写操作在进行，防止串口冲突
    private static readonly SemaphoreSlim _ioLock = new SemaphoreSlim(1, 1);

    //Modbus 从站ID,默认为1
    private const byte SlaveID = 1;


    //当读取到的新的数据时候触发该事件，用于Ui层显示
    public static event EventHandler<DeviceStates>?DataReceived;


    //当连接状态发生变化时触发该事件(true连上 false断开)
    public static event EventHandler<bool>? ConnectionChanged;

    //只读属性，表示当前是否已经连接上PLC
    public static bool IsConnected =>
    (_serialPort != null && _serialPort.IsOpen) ||
    (_tcpClient != null && _tcpClient.Connected);

    //获取当前系统可用的串口列表
    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();



    public static async Task Initialize(DeviceSettings Settings)
    {
        //先断开旧的连接，如果有的，防止资源泄露或重复开闭串口

        await DisConnectAsync();
        if (Settings is not { AutoConnect: true })  return; 
          switch(Settings.Mode)
        {
            case ConnectionMode.Serial:
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
                break;
            case ConnectionMode.ModbusTcp:
                await ConnectModbusTcpAsync(Settings.TcpIp, Settings.TcpPort);
                break;
            case ConnectionMode.CustomTcp:
                //不做任何事，由调用方自行 new TcpConnection
                LogService.Info("CustomTcp 模式需由调用方自行管理 TcpConnection");
                break;
           


        }

        }

    private static async Task ConnectModbusTcpAsync(string tcpIp, int tcpPort)
    {
        // 先保存参数，无论连接是否成功，PollDataLoop 启动后都能重连
        _currentMode = ConnectionMode.ModbusTcp;
        _lastTcpIp = tcpIp;
        _lastTcpPort = tcpPort;

        var client = new TcpClient();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var connectTask = client.ConnectAsync(tcpIp, tcpPort);
            if(await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, cts.Token)) != connectTask)
            {
                client.Dispose();
                throw new OperationCanceledException("TCP 连接超时", cts.Token);
            }
            await connectTask;
            _ipMaster = ModbusIpMaster.CreateIp(client);
            _ipMaster.Transport.ReadTimeout = 1000;
            _ipMaster.Transport.WriteTimeout = 1000;
            // Socket 级超时（NetworkStream.ReadTimeout 对异步读无效）
            client.Client.ReceiveTimeout = 1000;
            client.Client.SendTimeout = 1000;
            _tcpClient = client;  // 保存引用，断开时释放
            LogService.Info($"Modbus TCP 连接成功 {tcpIp}:{tcpPort}，等待首次数据确认...");
            // 不在这里触发 ConnectionChanged——由 PollDataLoop 首次读到数据时确认为"在线"
            _cts = new CancellationTokenSource();
            _= Task.Run(() => PollDataLoop(_cts.Token));
        }
        catch (Exception ex)
        {
            LogService.Error($"Modbus TCP 连接失败 {tcpIp}:{tcpPort}", ex);
            client.Dispose();
            ConnectionChanged?.Invoke(null, false);
            // 连接失败也启动轮询，在后台自动重连
            _cts = new CancellationTokenSource();
            _= Task.Run(() => PollDataLoop(_cts.Token));
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

        // 保存参数，无论连接是否成功，PollDataLoop 启动后都能重连
        _currentMode = ConnectionMode.Serial;
        _lastPortName = _serialPort.PortName;
        _lastBaudRate = _serialPort.BaudRate;
        _lastDataBits = _serialPort.DataBits;
        _lastParity = _serialPort.Parity;
        _lastStopBits = _serialPort.StopBits;

        try
        {
            // 带超时的串口打开（效仿 TCP 的 5 秒 ConnectionTimeout）
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Task.Run(() => _serialPort.Open(), cts.Token);

            _modbusMaster = ModbusSerialMaster.CreateRtu(_serialPort);
            _modbusMaster.Transport.ReadTimeout = 1000;
            _modbusMaster.Transport.WriteTimeout = 1000;
            // 串口级超时
            _serialPort.ReadTimeout = 1000;
            _serialPort.WriteTimeout = 1000;

            LogService.Info($"PLC串口打开成功 {_serialPort.PortName}，等待首次数据确认...");
            // 不在这里触发 ConnectionChanged(true)——由 PollDataLoop 首次读到数据时确认为"在线"

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => PollDataLoop(_cts.Token));
        }
        catch (Exception ex)
        {
            LogService.Warn($"串口连接失败 {_serialPort.PortName}，将进入后台重连: {ex.Message}");
            // 连接失败时不触发 ConnectionChanged(false)，让 UI 保持"未知"状态
            // PollDataLoop 会在连续失败后触发
            // 连接失败也启动轮询，在后台自动重连（效仿 TCP）
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => PollDataLoop(_cts.Token));
        }
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
                var old = _serialPort;
                _serialPort = null;
                try { if (old.IsOpen) old.Close(); } catch { }
                try { old.Dispose(); } catch { }
            }

            if (_modbusMaster != null)
            {

                _modbusMaster.Dispose();
                _modbusMaster = null;

            }
            // 先释放上层 Modbus Master，再释放底层 TCP Client
            _ipMaster?.Dispose();
            _ipMaster = null;
            if (_tcpClient != null)
            {
                if (_tcpClient.Connected) _tcpClient.Close();
                _tcpClient.Dispose();
                _tcpClient = null;
            }
            // 清理重连参数
            _currentMode = null;
            _lastTcpIp = null;
            _lastTcpPort = 0;
            _lastPortName = null;
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
                if (!IsConnected || (_tcpClient != null && !IsTcpReallyConnected()) || (_serialPort != null && !IsSerialReallyConnected()))
                {
                    if (IsConnected) ConnectionChanged?.Invoke(null, false);
                    // 根据当前模式选择重连方式
                    if (_currentMode == ConnectionMode.ModbusTcp)
                        await TryReconnectTcpAsync();
                    else if (_currentMode == ConnectionMode.Serial)
                        await TryReconnectSerialAsync();
                    if (!IsConnected)
                    {
                        await Task.Delay(1000, token);
                        continue;
                    }
                    errCount = 0;
                }
                // 心跳：ReadStateAsync 限时 2 秒，超时认为连接断开
                var readTask = ReadStateAsync();
                var timeoutTask = Task.Delay(2000, token);
                if (await Task.WhenAny(readTask, timeoutTask) == timeoutTask)
                {
                    // 不抛弃 readTask——等它完成（避免 UnobservedTaskException）
                    readTask.ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);
                    throw new TimeoutException("Modbus 读取超时（2s），连接可能已断开");
                }

                var state = await readTask;
                if (state != null)
                {
                    errCount = 0;
                    ConnectionChanged?.Invoke(null, true);
                    DataReceived?.Invoke(null, state);
                }
                await Task.Delay(200, token);

            }
            catch (OperationCanceledException)
            {
                break;//收到取消请求，跳出循环
            }

            catch (Exception ex)
            {
                errCount++;
                if (errCount >= 3)
                {
                    LogService.Debug($"PLC通讯异常:{ex.Message}");
                    errCount = 0;
                    // 根据当前模式强制重连
                    if (_currentMode == ConnectionMode.ModbusTcp)
                        await TryReconnectTcpAsync(force: true);
                    else if (_currentMode == ConnectionMode.Serial)
                        await TryReconnectSerialAsync(force: true);
                    if (IsConnected)
                    {
                        // 重连成功，立即重试读数据，不等 1 秒
                        continue;
                    }
                    ConnectionChanged?.Invoke(null, false);
                }

                await Task.Delay(1000, token);//等待一段时间后重试

            }


        }
    }
    private static IModbusMaster? GetCurrentMaster() =>
    _modbusMaster ?? (IModbusMaster?)_ipMaster; //使得ReadStateAsync可以动态选 Master：

    // 检测串口是否真正存活（IsOpen 可能过时）
    private static bool IsSerialReallyConnected()
    {
        try
        {
            return _serialPort != null && _serialPort.IsOpen;
        }
        catch { return false; }
    }

    // 检测 TCP 是否真正存活（TcpClient.Connected 不可靠）
    private static bool IsTcpReallyConnected()
    {
        try
        {
            if (_tcpClient?.Client == null) return false;
            var socket = _tcpClient.Client;
            // 连接异常（RST）
            if (socket.Poll(0, SelectMode.SelectError)) return false;
            // 远端正常关闭（FIN）— 可读且无数据
            if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0) return false;
            return true;
        }
        catch { return false; }
    }

    // 串口断开后轻量重连（效仿 TCP 的 TryReconnectTcpAsync）
    private static async Task TryReconnectSerialAsync(bool force = false)
    {
        if (!force && IsSerialReallyConnected()) return;
        if (string.IsNullOrEmpty(_lastPortName)) return;
        try
        {
            // 释放旧连接
            if (_modbusMaster != null)
            {
                _modbusMaster.Dispose();
                _modbusMaster = null;
            }
            if (_serialPort != null)
            {
                var old = _serialPort;
                _serialPort = null;
                try { if (old.IsOpen) old.Close(); } catch { }
                try { old.Dispose(); } catch { }
                // 等 Windows 释放端口句柄
                await Task.Delay(200);
            }
            // 建新连接
            var sp = new SerialPort(_lastPortName, _lastBaudRate, _lastParity, _lastDataBits, _lastStopBits)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Task.Run(() => sp.Open(), cts.Token);
            _serialPort = sp;
            _modbusMaster = ModbusSerialMaster.CreateRtu(sp);
            _modbusMaster.Transport.ReadTimeout = 1000;
            _modbusMaster.Transport.WriteTimeout = 1000;
            LogService.Debug($"串口重连成功 {_lastPortName}，等待首次数据确认...");
        }
        catch (Exception ex)
        {
            LogService.Warn($"串口重连失败: {ex.Message}");
        }
    }

    // TCP 断开后轻量重连：只重建 TcpClient + _ipMaster，不动 _cts，不启新轮询
    private static async Task TryReconnectTcpAsync(bool force = false)
    {
        if (!force && _ipMaster != null && _tcpClient?.Connected == true) return; // 没断，不用重连
        if (string.IsNullOrEmpty(_lastTcpIp) || _lastTcpPort <= 0) return;
        try
        {
            // 释放旧连接
            _ipMaster?.Dispose();
            _ipMaster = null;
            if (_tcpClient != null)
            {
                try { _tcpClient.Close(); _tcpClient.Dispose(); } catch { }
                _tcpClient = null;
            }
            // 建新连接
            var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var connectTask = client.ConnectAsync(_lastTcpIp, _lastTcpPort);
            if (await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, cts.Token)) != connectTask)
            {
                client.Dispose();
                return;
            }
            await connectTask;
            _ipMaster = ModbusIpMaster.CreateIp(client);
            _ipMaster.Transport.ReadTimeout = 1000;
            _ipMaster.Transport.WriteTimeout = 1000;
            client.Client.ReceiveTimeout = 1000;
            client.Client.SendTimeout = 1000;
            _tcpClient = client;
            LogService.Debug($"TCP 重连成功 {_lastTcpIp}:{_lastTcpPort}，等待首次数据确认...");
        }
        catch (Exception ex)
        {
            LogService.Warn($"TCP 重连失败: {ex.Message}");
        }
    }



    //从PLC读取当前设备状态并且封装为DeviceState对象返回
    public static async Task <DeviceStates> ReadStateAsync()
    {
       await _ioLock.WaitAsync();
        try
        {
            // 串口 Modbus RTU 路径 —— 与快照版本一字不差
            if (_modbusMaster != null)
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
                    CurrentTemp = Math.Round(registers[ModbusConfigHelper.CurrentTemp]/100.0, 2),
                    SettingTemp = Math.Round(registers[ModbusConfigHelper.SettingTemp]/100.0, 2),
                    RunningTime = Math.Round(registers[ModbusConfigHelper.RunningTime]/100.0, 2),
                    CurrentCycleTime = Math.Round(registers[ModbusConfigHelper.CurrentCycleTime]/100.0, 2),
                    StandardCycleTime = Math.Round(registers[ModbusConfigHelper.StandardCycleTime]/100.0, 2),
                    LiquidLevel = Math.Round(registers[ModbusConfigHelper.LiquidLevel]/100.0, 2),
                    ValueOpen = registers[ModbusConfigHelper.ValueOpen] == 1,//数字1表示打开阀门，0表示关闭
                    BarCode = barcode


                };
            }

            // 网口 Modbus TCP 路径
            if (_ipMaster != null)
            {
                ushort[] registers = await _ipMaster.ReadHoldingRegistersAsync(SlaveID, 0, 10);
                const ushort barcodeStart = 10;
                const ushort barcodeLength = 10;
                string barcode = string.Empty;
                try
                {
                    ushort[] barcodeRes = await _ipMaster.ReadHoldingRegistersAsync(SlaveID, barcodeStart,barcodeLength);
                barcode = ConverRegisterToString(barcodeRes);
                }
                catch (Exception ex)
                {
                    LogService.Warn($"读取条码失败: {ex.Message}");
                }
                return new DeviceStates
                {
                    ActualCount = registers[ModbusConfigHelper.ActualCount],
                    TargetCount = registers[ModbusConfigHelper.TargetCount],
                    CurrentTemp = Math.Round(registers[ModbusConfigHelper.CurrentTemp]/100.0, 2),
                    SettingTemp = Math.Round(registers[ModbusConfigHelper.SettingTemp]/100.0, 2),
                    RunningTime = Math.Round(registers[ModbusConfigHelper.RunningTime]/100.0, 2),
                    CurrentCycleTime = Math.Round(registers[ModbusConfigHelper.CurrentCycleTime]/100.0, 2),
                    StandardCycleTime = Math.Round(registers[ModbusConfigHelper.StandardCycleTime]/100.0, 2),
                    LiquidLevel = Math.Round(registers[ModbusConfigHelper.LiquidLevel]/100.0, 2),
                    ValueOpen = registers[ModbusConfigHelper.ValueOpen] == 1,
                    BarCode = barcode
                };
            }

            throw new InvalidOperationException("未连接");
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
        // 先快速检查，避免没 PLC 时白白等待 _ioLock
        if (GetCurrentMaster() == null)
        {
            LogService.Debug($"写入指令跳过（未连接PLC）:{command}={value}");
            return;
        }
        ushort address = command == "Start" ? (ushort)1 : (ushort)2;
        // 锁等待最多 500ms，超时就跳过——说明正在读操作中，等也没意义
        if (!await _ioLock.WaitAsync(500))
        {
            LogService.Debug($"写入指令跳过（锁超时）:{command}={value}");
            return;
        }
        try
        {
            // 锁内再次检查——等待期间可能被 DisConnectAsync 释放了
            var master = GetCurrentMaster();
            if (master == null)
            {
                LogService.Debug($"写入指令跳过（锁等待期间PLC断开）:{command}={value}");
                return;
            }
            await master.WriteSingleRegisterAsync(SlaveID, address, (ushort)(value ? 1 : 0));
            LogService.Debug($"写入指令:{command}={value}");
        }
        catch (Exception ex)
        {
            LogService.Error($"写入指令失败:{command}={value}", ex);
        }
        finally { _ioLock.Release(); }
    }



}
