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
        public static IFreeSql Fsql { get; private set; }//提供一个静态属性Fsql，表示数据库连接对象，可以在应用程序的任何地方通过DbProvider.Fsql来访问数据库

        public static void Initialize(string connectionString, FreeSql.DataType data = FreeSql.DataType.Sqlite)
        {
            if (Fsql != null) return;
            lock (_lock)
            {
                if (Fsql != null) return;

                Fsql = new FreeSql.FreeSqlBuilder()
                    .UseConnectionString(FreeSql.DataType.Sqlite, connectionString)//指定数据库类型和连接字符串，创建一个新的FreeSqlBuilder对象，并调用UseConnectionString方法来设置数据库连接信息
                    .UseAutoSyncStructure(true)//自动同步实体结构到数据库，如果实体类有变化会自动修改数据库表结构，适合开发阶段使用，生产环境建议关闭
                    .UseMonitorCommand
                    (cmd =>
                    {
                    },
                    (cmd, traceLog) =>
                    {
                        Serilog.Log.Verbose($"[SQL] {cmd.CommandText}\r\n ->{traceLog}");
                    })//监视SQL命令的执行，可以在这里添加日志记录或性能分析等功能，这里使用Serilog记录调试级别的日志，输出SQL命令文本和执行跟踪信息
                    .UseLazyLoading(false)
                    .Build();
                // 开启WAL模式：读写并发，导出/查询不阻塞写入
                Fsql.Ado.ExecuteNonQuery("PRAGMA journal_mode=WAL;");
            }
        }
    }
}
