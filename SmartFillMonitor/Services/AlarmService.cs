using SmartFillMonitor.Models;
using SmartFillMonitor.Services.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SmartFillMonitor.Models.AlarmRecord;

namespace SmartFillMonitor.Services
{
    public class AlarmService//管理系统或设备的报警，并且提供操作数据的方法
    {

        public static event EventHandler<AlarmRecord>? AlarmTriggered;//定义一个静态事件，当有新的报警记录被触发时，其他部分可以订阅这个事件来播放或弹窗
        public static event EventHandler<AlarmRecord>? AlarmRecovered;//定义一个静态事件，当有报警记录消除时，其他部分可以订阅这个事件来消除报警红灯
        public static async Task TriggerAlarmAsync(AlarmRecord alarmRecord)//触发报警的方法，接受一个AlarmRecord对象作为参数，表示要触发的报警记录
        {
            try
            {
                bool isAlreadyActive = await DbProvider.Fsql.Select<AlarmRecord>()
                  .Where(a => a.Code == alarmRecord.Code && a.IsActive).AnyAsync();//查询数据库中是否已经有相同类型的活动报警记录，如果有则返回true，否则返回false
                if (isAlreadyActive) return; //如果已经有相同类型的活动报警，则不再触发新的报警记录
                await DbProvider.Fsql.Insert(alarmRecord).ExecuteAffrowsAsync();//将新的报警记录插入数据库中
                var latestRecord = await DbProvider.Fsql.Select<AlarmRecord>()
                    .Where(a => a.Code == alarmRecord.Code).FirstAsync();
                if(latestRecord !=null)
                {
                    alarmRecord = latestRecord;
                }
                LogService.Warn($"[报警触发]: {alarmRecord.Code}:{alarmRecord.Message}");//记录日志，输出报警的类型、时间和级别等信息
                AlarmTriggered.Invoke(null, alarmRecord);//通知UI触发报警事件，传递新的报警记录作为参数，让UI可以根据这个记录来显示报警信息和红灯等效果
            }
            catch (Exception ex)
            {
                LogService.Error($"触发报警时发生异常: {alarmRecord.Code}", ex);//如果在触发报警的过程中发生任何异常，记录错误日志，输出异常信息和堆栈跟踪等细节


            }
        }

        public static async Task RecoverAlarmAsync(AlarmCode alarmCode)//恢复报警的方法，比如PLC信号恢复正常时调用，接受一个AlarmCode枚举值作为参数，表示要恢复的报警类型
        {
            try
            {
                var activeAlarm = await DbProvider.Fsql.Select<AlarmRecord>()
                  .Where(a => a.Code == alarmCode && a.IsActive).FirstAsync();//查询数据库中是否有指定类型的活动报警记录，如果有则返回这个记录，否则返回null
                if (activeAlarm == null) return; //如果没有报警，直接返回
                activeAlarm.IsActive = false;//将这个报警记录的IsActive属性设置为false，表示这个报警已经消除
                activeAlarm.EndTime = DateTime.Now;//将这个报警记录的EndTime属性设置为当前时间，表示这个报警的结束时间
                activeAlarm.DurationSeconds = (activeAlarm.EndTime - activeAlarm.StartTime).TotalSeconds;

                await DbProvider.Fsql.Update<AlarmRecord>()
                  .SetSource(activeAlarm)//指定要更新的AlarmRecord对象，表示要将这个对象的属性值更新到数据库中
                  .UpdateColumns(a => new { a.IsActive, a.EndTime, a.DurationSeconds }).ExecuteAffrowsAsync();//指定要更新的列，这里只更新IsActive、EndTime和DurationSeconds三个列，表示只更新这三个属性值到数据库中
                LogService.Info($"[报警恢复]: {activeAlarm.Code}");
                AlarmRecovered?.Invoke(null, activeAlarm);//通知UI触发报警恢复事件，传递这个报警记录作为参数，让UI可以根据这个记录来消除报警信息和红灯等效果
            }
            catch (Exception ex)
            {
                LogService.Error($"恢复报警时发生异常: {alarmCode}", ex);//如果在恢复报警的过程中发生任何异常，记录错误日志，输出异常信息和堆栈跟踪等细节
            }


        }

        public static async Task<bool> AcknowledgeAlarmAsync(long alarmId, string operatorName)//点击确认报警的方法，接受一个报警ID和一个用户名作为参数，表示要确认的报警记录和操作人
        {
            try
            {
                var result = await DbProvider.Fsql.Update<AlarmRecord>()
                    .Set(a => a.IsAcknowledged, true)
                     .Set(a => a.AcknowledgedTime, DateTime.Now)
                      .Set(a => a.AcknowledgedUser, operatorName)
                      .Where(a => a.Id == alarmId && !a.IsAcknowledged)//防止重复确认
                      .ExecuteAffrowsAsync();
                if (result > 0)
                {

                    LogService.Info($"[报警确认]: ID={alarmId} by {operatorName}");//记录日志，输出确认的报警ID和操作人等信息
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {

                LogService.Error($"确认报警时发生异常: ID={alarmId}", ex);//如果在确认报警的过程中发生任何异常，记录错误日志，输出异常信息和堆栈跟踪等细节
                return false;
            }



        }

        public static async Task<List<AlarmRecord>> GetActiveAlarmsAsync()//获取当前所有活动报警的方法，返回一个包含AlarmRecord对象的列表，表示当前所有未恢复的报警记录
        {
            return await DbProvider.Fsql.Select<AlarmRecord>()
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();




        }

        public static async Task<(List<AlarmRecord> Items, long Total)> GetAlarmHistoryAsync(int pageIndex, int pageSize, DateTime? startTime = null, DateTime? endTime = null, AlarmSeverity alarmSeverity = AlarmSeverity.All)//获取报警历史记录的方法，接受分页参数、时间范围和报警级别等过滤条件，返回一个包含AlarmRecord对象的列表和总记录数的元组，表示符合条件的报警历史记录和总数
        {
            try
            {
                var query = DbProvider.Fsql.Select<AlarmRecord>();//从数据库中查询AlarmRecord表，返回一个查询对象，可以在这个对象上继续添加过滤条件和排序等操作
                if (startTime.HasValue)
                {
                    query = query.Where(a => a.StartTime >= startTime.Value);
                }
                if (endTime.HasValue)
                {
                    query = query.Where(a => a.EndTime <= endTime.Value);
                }
                if (alarmSeverity != AlarmSeverity.All)
                {
                    query = query.Where(a => a.Severity == alarmSeverity);//如果指定了报警级别过滤条件，则在查询中添加一个条件，筛选出符合指定级别的报警记录
                }

                var total = await query.CountAsync();
                var items = await query.OrderByDescending(a => a.StartTime)
                    .Page(pageIndex, pageSize)
                    .ToListAsync();//根据分页参数获取指定页的数据，按照开始时间降序排序
                return (items, total);
            }
            catch (Exception ex)
            {

                LogService.Error($"获取报警历史记录时发生异常", ex);//如果在获取报警历史记录的过程中发生任何异常，记录错误日志，输出异常信息和堆栈跟踪等细节
                return (new List<AlarmRecord>(), 0);//如果发生异常，返回一个空的列表和总数为0的元组，表示没有符合条件的报警历史记录
            }

        }
    }
}