using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
namespace SmartFillMonitor.Services
{
   public static  class PlcService
    {
        public static string[] GetAvailablePorts() => SerialPort.GetPortNames();

    }
}
