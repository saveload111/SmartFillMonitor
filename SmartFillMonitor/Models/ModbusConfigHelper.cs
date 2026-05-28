using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFillMonitor.Models
{
 public   class ModbusConfigHelper
    {
        public static readonly int ActualCount = 0;

        public static readonly int TargetCount = 1;//目标产量



        public static readonly int CurrentTemp = 2;//当前实时温度





        public static readonly int SettingTemp = 3;//设定温度



        public static readonly int RunningTime = 4;//运行时间


        public static readonly int CurrentCycleTime = 5;//当前节拍





        public static readonly int StandardCycleTime = 6;//总节拍




        public static readonly int LiquidLevel = 7;//当前液位



        public static readonly int ValueOpen = 8;//当前阀门



      


    }
}
