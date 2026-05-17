using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFillMonitor.Models
{
  public class DeviceSettings
    {
        //串口配置
        public string PortName { get; set; }= "COM1";
        public int BaudRate { get; set; } = 115200;
        public int DataBit { get; set; } = 8;
        public string StopBit { get; set; } = "One";
        public string Parity { get; set; } = "None";

        //系统选项
        public bool AutoConnect { get; set; } = true;

        public bool AlarmSound { get; set; } = true;

        public bool DebugLogMode { get; set; } = false;





    }
}
