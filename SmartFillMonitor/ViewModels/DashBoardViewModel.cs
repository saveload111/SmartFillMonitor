using CommunityToolkit.Mvvm.ComponentModel;
using SmartFillMonitor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiveCharts;
using LiveCharts.Wpf;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using SmartFillMonitor.Services;
using System.Windows;
using SmartFillMonitor.Services.Logs;
namespace SmartFillMonitor.ViewModels
{
    public partial class DashBoardViewModel:ObservableObject
    {
        [ObservableProperty]

        private int actualCount;

        [ObservableProperty]

        private int targetCount;

        [ObservableProperty]

        private double currentTemp;

  

        [ObservableProperty]
        private LightState indicatorState = LightState.Off;

        [ObservableProperty]

        private double settingTemp;


        [ObservableProperty]

        private double runningTime;

        [ObservableProperty]

        private double currentCycleTime;



        [ObservableProperty]

        private double standardCycleTime;


        [ObservableProperty]

        private double liquidLevel;

        [ObservableProperty]

        private bool valueOpen = true;

        [ObservableProperty]

        private SeriesCollection tempLiveCharts;

        [ObservableProperty]
        private string deviceStatus = "自动运行";

        private string _lastbarCode = string.Empty;

        public ObservableCollection<AlarmUIModel> RecentAlarms { get; } = new ObservableCollection<AlarmUIModel>();

        public DashBoardViewModel()
        {
            PlcService.DataReceived += OnDataReceived;
            PlcService.ConnectionChanged += OnConnectionChanged;
            AlarmService.AlarmTriggered += AlarmService_AlarmTriggered;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                TempLiveCharts = new SeriesCollection
                {
                    new LineSeries
                    {
                        Title = "实际温度",
                        Values = new ChartValues<double>(),
                        Stroke = Brushes.Cyan,
                        StrokeThickness = 2,
                        PointGeometry = null,
                    },
                    new LineSeries
                    {
                        Title = "设定温度",
                        Values = new ChartValues<double>(),
                        Stroke = Brushes.Orange,
                        StrokeThickness = 2,
                        PointGeometry = null,
                        StrokeDashArray = new DoubleCollection { 4, 2 },  // 虚线，一眼看出是设定值
                    }
                };
            });
        }

        private void OnConnectionChanged(object? sender, bool connected)
        {
            if (!connected)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ActualCount = 0;
                    TargetCount = 0;
                    CurrentTemp = 0;
                    SettingTemp = 0;
                    RunningTime = 0;
                    CurrentCycleTime = 0;
                    StandardCycleTime = 0;
                    LiquidLevel = 0;
                    ValueOpen = false;
                });
            }
        }

        private void AlarmService_AlarmTriggered(object? sender, AlarmRecord e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RecentAlarms.Insert(0, AlarmUIModel.FromAlarmRecord(e));
                if (RecentAlarms.Count > 10)
                {

                    RecentAlarms.RemoveAt(RecentAlarms.Count - 1);

                }
            });
            
            
            
         }



        private void OnDataReceived(object? sender, DeviceStates state)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    ActualCount = state.ActualCount;
                    TargetCount = state.TargetCount;
                    CurrentCycleTime = state.CurrentCycleTime;
                    SettingTemp = state.SettingTemp;
                    RunningTime = state.RunningTime;
                    CurrentTemp = state.CurrentTemp;
                    StandardCycleTime = state.StandardCycleTime;
                    LiquidLevel = state.LiquidLevel;
                    ValueOpen = state.ValueOpen;
                    var barcode = state.BarCode ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(barcode) && barcode != _lastbarCode)
                    {
                        _lastbarCode = barcode;
                        var record = new ProductionRecord
                        {
                            Time = DateTime.Now,
                            BatchNo = barcode,
                            SettingTemp = state.SettingTemp,
                            ActualCount = state.ActualCount,
                            ActualTemp = state.CurrentTemp,
                            TargetCount = state.TargetCount,
                            IsNG = false,
                            CycleTime = state.CurrentCycleTime,
                            Operator = ""
                        };
                        await DbProvider.Fsql.Insert(record).ExecuteAffrowsAsync();
                    }
                }
                catch (Exception ex)
                {
                    LogService.Error("OnDataReceived 处理失败", ex);
                }
            });

            
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (TempLiveCharts == null || TempLiveCharts.Count == 0) return;
                TempLiveCharts[0].Values.Add(state.CurrentTemp);
                if (TempLiveCharts[0].Values.Count > 40)
                    TempLiveCharts[0].Values.RemoveAt(0);

                if (TempLiveCharts.Count > 1)
                {
                    TempLiveCharts[1].Values.Add(state.SettingTemp);
                    if (TempLiveCharts[1].Values.Count > 40)
                        TempLiveCharts[1].Values.RemoveAt(0);
                }
            });
        }

        [RelayCommand]
        private  async Task StartProductionAsync()
        {
            try
            {
                DeviceStatus = "启动中...";
                IndicatorState = LightState.Green;
                await PlcService.WriteCommandStateAsync("Start", true);
                await Task.Delay(2000);
                DeviceStatus = "运行中...";
                LogService.Debug("发送启动命令到PLC");
            }
            catch (Exception ex)
            {
                DeviceStatus = "启动失败";
                IndicatorState = LightState.Red;
                LogService.Error("发送启动命令到PLC失败", ex);
            }

        }


        [RelayCommand]
        private async Task StopProductionAsync()
        {
            try
            {
                DeviceStatus = "停止中...";
                IndicatorState = LightState.Red;
                await PlcService.WriteCommandStateAsync("Stop", true);
                await Task.Delay(2000);
                DeviceStatus = "已停止";
                LogService.Debug("发送停止命令到PLC");
            }
            catch (Exception ex)
            {
                DeviceStatus = "停止失败";
                IndicatorState = LightState.Red;
                LogService.Error("发送停止命令到PLC失败", ex);
            }







        }

        [RelayCommand]
        private async Task ResetProductionAsync()
        {
            try
            {
                DeviceStatus = "复位中...";
                IndicatorState = LightState.Yellow;
                await PlcService.WriteCommandStateAsync("Stop", false);
                await Task.Delay(2000);
                await PlcService.WriteCommandStateAsync("Reset", false);
                DeviceStatus = "已就绪";
                IndicatorState = LightState.Off;
                LogService.Debug("发送复位脉冲到PLC");
            }
            catch (Exception ex)
            {
                DeviceStatus = "复位失败";
                IndicatorState = LightState.Red;
                LogService.Error("发送复位脉冲到PLC失败", ex);
            }



        }

    }
}
