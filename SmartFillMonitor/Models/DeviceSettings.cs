using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFillMonitor.Models
{
    public enum ConnectionMode
    {
        Serial,       // 串口 Modbus RTU
        ModbusTcp,    // 网口 Modbus TCP（NModbus IpMaster）
        CustomTcp     // 自定义 TCP 协议（TcpConnection 原生收发）
    }

    public class DeviceSettings
    {
        // === 连接模式 ===
        public ConnectionMode Mode { get; set; } = ConnectionMode.Serial;


        //串口配置 （Mode == Serial 时有效）===
        public string PortName { get; set; }= "COM1";
        public int BaudRate { get; set; } = 115200;
        public int DataBit { get; set; } = 8;
        public string StopBit { get; set; } = "One";
        public string Parity { get; set; } = "None";


        // === TCP 配置（Mode == ModbusTcp / CustomTcp 时有效）===（ModbusTcp / CustomTcp 共用）
        public string TcpIp { get; set; } = "127.0.0.1";
        public int TcpPort { get; set; } = 502;


        //系统选项
        public bool AutoConnect { get; set; } = true;

        public bool AlarmSound { get; set; } = true;

        public bool DebugLogMode { get; set; } = false;

       


    }
}
