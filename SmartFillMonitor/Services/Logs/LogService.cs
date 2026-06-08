using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFillMonitor.Services.Logs
{
  public static class LogService
    {
        public static void Info(string message) =>  Serilog.Log.Information(message);//直接调用Serilog的静态方法来记录日志，简化了日志记录的使用

        public static void Warn(string message) => Serilog.Log.Warning(message);

        public static void Debug(string message) => Serilog.Log.Debug(message);

        public static void Verbose(string message) => Serilog.Log.Verbose(message);

        public static void Fatal(string message) => Serilog.Log.Fatal(message);

        public static void Fatal(string message,Exception ex) => Serilog.Log.Fatal(ex, message);
  
        public static void Error(string message,Exception ex=null) => Serilog.Log.Error(ex, message);
    }
}
