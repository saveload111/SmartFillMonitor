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
                LogService.Error($"新报警:  {alarm.Code} - {alarm.Title} ");
            });
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
        private async Task AcknowledgeAlarmAsync(AlarmUIModel alarm)// 确认报警的命令，绑定到UI上的确认按钮，接受一个AlarmUIModel对象作为参数，表示要确认的报警记录
        {
            if (alarm == null) return;
            try
            {
                var success = await AlarmService.AcknowledgeAlarmAsync(alarm.Id,"");//调用AlarmService的AcknowledgeAlarmAsync方法来确认这个报警记录，传递报警ID和操作人名称作为参数
                if (success)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {



                        ActiveAlarms.Remove(alarm);//如果确认成功，从ActiveAlarms集合中移除这个报警记录，这样UI上就不会再显示这个报警了
                        ActiveAlarmCount = ActiveAlarms.Count;

                    });
                }
            }
            catch (Exception ex)
            {


                LogService.Error($"确认报警异常: {alarm.Code}", ex);//如果在确认报警的过程中发生任何异常，记录错误日志，输出异常信息和堆栈跟踪等细节
            }
        }
    }
}