using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FreeSql.DataAnnotations;
namespace SmartFillMonitor.Models
{


    [Table (Name ="Users")]
    [Index("idx_unique_username","Username",true)]
    public class User
    {
        [Column(IsPrimary = true,IsIdentity =true)]
        public long Id { get; set; }

        [Column(StringLength=50,IsNullable =false)]
        public string UserName { get; set; }

        [Column(StringLength = 50)]
        public string DisplayName { get; set; }
        //存储哈希后的密码
        [Column(StringLength = 50, IsNullable = false)]
        public string PasswordHash { get; set; }



        [Column (MapType =typeof(int))]
        public Role Role { get; set; }

        public bool IsDisabled { get; set; }=false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLoginTime { get; set; }
        //方便UI绑定，（不映射到数据库）
        [Column (IsIgnore=true)]
        public string RoleName => Role switch
        {
            Role.Admin => "管理员",
            Role.Engineer => "工程师",
            Role.Operator => "操作员",
            _=>"未知"
        };

      

    }
    public enum Role
    {
        [Description("管理员")]
        Admin = 0,
        [Description("工程师")]
        Engineer = 1,
        [Description("操作员")]
        Operator = 2,
    }











}
