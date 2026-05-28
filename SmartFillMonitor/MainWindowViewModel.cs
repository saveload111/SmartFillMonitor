using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SmartFillMonitor.ViewModels;
using SmartFillMonitor.UserControls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using HandyControl.Controls;

namespace SmartFillMonitor
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private object mainContent;// 当前显示的ViewModel，绑定到UI上显示
        private readonly IServiceProvider _serviceProvider;// 用于解析其他ViewModel的依赖
        private readonly DispatcherTimer _timer;
        [ObservableProperty]
        private string currentTime;// 当前时间字符串, 绑定到UI上显示，小写自动转大写
        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
            MainContent = _serviceProvider.GetRequiredService<DashBoardViewModel>();
        }

        
        private void Timer_Tick(object? sender, EventArgs e)
        { 
         CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        #region Navigation Commands
        [RelayCommand]
        private void Navigate(string? destination)
        { 
            if(destination == null) return;
            switch (destination)
            {   
                case "Dashboard":
                    MainContent = _serviceProvider.GetRequiredService<DashBoardViewModel>();
                    break;
                case "DataQuery":
                    MainContent = _serviceProvider.GetRequiredService<DashQueryViewModel>();
                    break;
                case "Logs":
                    MainContent = _serviceProvider.GetRequiredService<LogsViewModel>();
                    break;
                case "Alarms":
                    MainContent = _serviceProvider.GetRequiredService<AlarmsViewModel>();
                    break;
                case "Settings":
                    MainContent = _serviceProvider.GetRequiredService<SettingViewModel>();
                    break;
                    default:
                    break;
            }
        }


        #endregion
    }
}
