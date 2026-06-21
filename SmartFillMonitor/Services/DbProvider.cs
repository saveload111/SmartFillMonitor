using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFillMonitor.Services
{
    public static class DbProvider
    {
        private static readonly object _lock = new object();
        public static IFreeSql Fsql { get; private set; }

        public static void Initialize(string connectionString, FreeSql.DataType data = FreeSql.DataType.Sqlite)
        {
            if (Fsql != null) return;
            lock (_lock)
            {
                if (Fsql != null) return;

                Fsql = new FreeSql.FreeSqlBuilder()
                    .UseConnectionString(FreeSql.DataType.Sqlite, connectionString)
                    .UseAutoSyncStructure(true)
                    .UseMonitorCommand
                    (cmd =>
                    {
                    },
                    (cmd, traceLog) =>
                    {
                        Serilog.Log.Verbose($"[SQL] {cmd.CommandText}\r\n ->{traceLog}");
                    })
                    .UseLazyLoading(false)
                    .Build();
            }
        }
    }
}
