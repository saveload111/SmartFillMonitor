using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeSql.DataAnnotations;

namespace SmartFillMonitor.Models
{
    [Table(Name = "SystemLog", DisableSyncStructure=true)]//使用FreeSql的Table特性来定义一个数据库表，指定表名为SystemLog，并禁用自动同步结构，因为我们不需要在数据库中创建这个表
    public class SystemLog
    {
        [Column(Name = "Id", IsIdentity = true, IsPrimary = true)]//使用FreeSql的Column特性来定义一个数据库列，指定该列为自增列和主键
        public int Id { get; set; }

        [Column(Name = "Timestamp")]//使用FreeSql的Column特性来定义一个数据库列，指定该列为时间戳列
        public DateTime Timestamp { get; set; }

        [Column(Name = "Level", StringLength =50)]//使用FreeSql的Column特性来定义一个数据库列，指定该列为日志级别列
        public string Level { get; set; }

        [Column(Name = "Exception", StringLength = 1000)]//使用FreeSql的Column特性来定义一个数据库列，指定该列为异常信息列
        public string Exception { get; set; }

        [Column(Name = "RenderedMessage", StringLength = 50)]//使用FreeSql的Column特性来定义一个数据库列，指定该列为日志消息列
        public string RenderedMessage { get; set; }

        [Column(Name = "Properties", StringLength = 1000)]//使用FreeSql的Column特性来定义一个数据库列，指定该列为属性列
        public string Properties { get; set; }

    }
}
