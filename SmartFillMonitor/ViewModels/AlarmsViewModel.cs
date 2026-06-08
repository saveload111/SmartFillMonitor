using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Logs;
using static SmartFillMonitor.Models.AlarmRecord;
namespace SmartFillMonitor.ViewModels
{
    public partial class AlarmsViewModel : ObservableObject// 报警页面的ViewModel，负责管理报警数据和与UI的交互
    {
        public ObservableCollection<AlarmUIModel> ActiveAlarms { get; }// 当前活动的报警列表，绑定到UI上显示
        public ObservableCollection<AlarmUIModel> HistoryAlarms { get; }
        [ObservableProperty]
        private int activeAlarmCount;// 当前活动报警的数量，绑定到UI上显示
        [ObservableProperty]
        private DateTime historyStartDate = DateTime.Today.AddDays(-1);// 历史报警的查询起始时间，绑定到UI上的日期选择控件
        [ObservableProperty]
        private DateTime historyEndDate = DateTime.Today;// 历史报警的查询结束时间，绑定到UI上的日期选择控件

        public AlarmsViewModel()
        {

            ActiveAlarms = new ObservableCollection<AlarmUIModel>();

            AlarmService.AlarmTriggered += OnAlarmTriggered;//订阅AlarmService的AlarmTriggered事件，当有新的报警记录被触发时，调用OnAlarmTriggered方法来处理这个事件，比如将新的报警记录添加到ActiveAlarms集合中，并更新ActiveAlarmCount属性的值
            HistoryAlarms = new ObservableCollection<AlarmUIModel>();
            _ = LoadActiveAlarmsAsync();

        }

        private async Task LoadActiveAlarmsAsync()// 加载当前活动报警数据的方法，通常在ViewModel初始化时调用
        {
            try
            {
                var records = await AlarmService.GetActiveAlarmsAsync();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ActiveAlarms.Clear();
                    foreach (var record in records)
                    {
                        ActiveAlarms.Add(AlarmUIModel.FromAlarmRecord(record));
                    }
                    ActiveAlarmCount = ActiveAlarms.Count;
                });
            }
            catch (Exception ex)
            {

                LogService.Error("加载活动报警异常", ex);
            }
        }

        private void OnAlarmTriggered(object? sender, AlarmRecord record)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var alarm = AlarmUIModel.FromAlarmRecord(record);
                ActiveAlarms.Insert(0, alarm);// 将新的报警记录添加到ActiveAlarms集合的开头，这样最新的报警会显示在列表的顶部
                ActiveAlarmCount = ActiveAlarms.Count;
                LogService.Warn($"新报警:  {alarm.Code} - {alarm.Title} ");
            });
        }

        [RelayCommand]
        private async Task TestAlarmAsync()
        {
            // 先恢复旧的测试报警
            await AlarmService.RecoverAlarmAsync(AlarmCode.HighTemperature);

            var record = new AlarmRecord
            {
                Code = AlarmCode.HighTemperature,
                Severity = AlarmSeverity.Error,
                StartTime = DateTime.Now,
                IsActive = true,
                Description = "测试报警：加热温度过高",
                Message = $"测试触发时间：{DateTime.Now:HH:mm:ss}"
            };
            await AlarmService.TriggerAlarmAsync(record);
        }

        [RelayCommand]
        private async Task RefreshAsync()// 刷新报警数据的命令，绑定到UI上的刷新按钮
        {
            await LoadActiveAlarmsAsync();

        }
        [RelayCommand]
        private async Task LoadHistoryAlarmsAsync()
        {

            var records = await AlarmService.GetAlarmHistoryAsync(1, 20, HistoryStartDate, HistoryEndDate.AddDays(1), AlarmSeverity.All);
            Application.Current.Dispatcher.Invoke(() =>
            {
                HistoryAlarms.Clear();
                foreach (var record in records.Items)
                {
                    HistoryAlarms.Add(AlarmUIModel.FromAlarmRecord(record));
                }
            });

        }
        [RelayCommand]
        private async Task AcknowledgeAlarmAsync(AlarmUIModel alarm)
        {
            if (alarm == null) return;
            try
            {
                var op = UserService.CurrentUser?.UserName ?? "";
                await AlarmService.AcknowledgeAlarmAsync(alarm.Id, op);

                // 解析报警码并恢复
                var codeStr = alarm.Code?.Replace("E", "");
                if (int.TryParse(codeStr, out var codeInt))
                {
                    await AlarmService.RecoverAlarmAsync((AlarmCode)codeInt);
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ActiveAlarms.Remove(alarm);
                    ActiveAlarmCount = ActiveAlarms.Count;
                });
            }
            catch (Exception ex)
            {
                LogService.Error($"确认报警异常: {alarm.Code}", ex);
            }
        }
    }
}