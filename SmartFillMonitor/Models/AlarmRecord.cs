using CommunityToolkit.Mvvm.ComponentModel;
using FreeSql.DataAnnotations;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
namespace SmartFillMonitor.Models
{
    #region //数据库实体类型

    [Table(Name = "AlarmRecord")]
    public class AlarmRecord
    {
        [Column(IsPrimary = true, IsIdentity = true)]
        public long Id { get; set; }
        //报警类型
        public AlarmCode Code { get; set; }
        //报警级别
        public AlarmSeverity Severity { get; set; }
        //报警开始时间
        public DateTime StartTime { get; set; }= DateTime.Now;
        //报警恢复（设备解除故障）时间
        public DateTime EndTime { get; set; }
        //报警持续时间，单位秒 
        public double? DurationSeconds { get; set;}
        //是否为活动报警（True表示当前仍在持续的报警，False表示已经结束的报警）
        public bool IsActive { get; set; } = true;
        //是否已被用户确认
        public bool IsAcknowledged { get; set; } = false;

        public DateTime? AcknowledgedTime { get; set; }

        //确认操作人（记录用户名）
        public string? AcknowledgedUser { get; set; }

        //处理建议（通常从enum获取描述信息）   
        public string? Description { get; set; }
        //动态信息（可以包含温度、设备状态、传感器读数等有助于诊断和处理报警的信息）
        public string? Message { get; set; }
        public enum AlarmSeverity  
        {
            [Description("所有")]
            All = 0,
            [Description("提示")]
            Info = 1,
            [Description("警告")]
            Warning = 2,
            [Description("错误")]
            Error = 3,
            [Description("致命")]
            Critical = 4
        }


        
        public enum AlarmCode
        {
            [Description("无")]
            None = 0,
            [Description("原料桶液位过低")]
            LowLiquidLevel = 1001,
            [Description("原料桶溢出")]
            Overfill = 1002,
            [Description("原料桶泄漏")]
            Leak = 1003,
            [Description("压缩空气压力过低")]
            LowAirPressure = 2001,
            [Description("加热温度过高")]
            HighTemperature = 3001,
            [Description("传感器故障")]
            SensorFailure = 4001,
            [Description("PLC通信故障")]
            CommunicationError = 5001,
            [Description("系统内部错误")]
             SystemError = 6001
        }
    }
    #endregion

    #region //UI视图模型，用于显示在界面
    public class  AlarmUIModel:INotifyPropertyChanged

    {
        private long _id;
        private string _code;
        private string _description;
        private string _title;
        private string _timeStr;

        public long Id

        {
            get =>_id;

            set
            {
                if(value == _id) return;
                _id = value;
                OnPropertyChanged();
            }


        }

        public string  Title

        {
            get => _title;

            set
            {
                if (value == _title) return;
                _title = value;
                OnPropertyChanged();
            }


        }
        public string Code
        {
            get => _code;
            set
            {
                if (value == _code) return;
                _code = value;
                OnPropertyChanged();
            }

             }
        public string Description
        {
            get => _description;
            set
            {
                if (value == _description) return;
                _description = value;
                OnPropertyChanged();
            }
        }
            
              
        public string TimeStr

{
            get => _timeStr;

            set
            {
                if (value == _timeStr) return;
                _timeStr = value;
                OnPropertyChanged();
            }


        }
       public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string?propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static AlarmUIModel FromAlarmRecord(AlarmRecord record)// 工厂方法，将AlarmRecord转换为AlarmUIModel
        {
          var title = record.Code.GetDescription(); // 获取报警级别的描述作为标题
            return new AlarmUIModel
            {
                Id = record.Id,
                Code = $"E{(int)record.Code}",// 将枚举值转换为字符串显示，例如E1001
                Title = title, // 使用获取到的标题

                Description = record.Description,
                TimeStr = record.StartTime.ToString("MM-dd HH:mm:ss")
            };
        }


    }


    #endregion
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());// 获取枚举成员的字段信息
            var attribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;// 获取Description特性，如果存在的话
            return attribute?.Description ?? value.ToString();// 返回Description特性的描述信息，如果没有则返回枚举成员的名称

        }
        
           
    }
}
