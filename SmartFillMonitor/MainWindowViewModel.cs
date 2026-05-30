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
using SmartFillMonitor.Models;
using SmartFillMonitor.Services;
using System.Windows;
using SmartFillMonitor.Views;

namespace SmartFillMonitor
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private object mainContent;// 当前显示的ViewModel，绑定到UI上显示
        [ObservableProperty]
        private object currentBatchNo;
        [ObservableProperty]
        private string currentTime;// 当前时间字符串, 绑定到UI上显示，小写自动转大写
        [ObservableProperty]
        private bool isUserLoggedIn;
        [ObservableProperty]
        private bool isPlcConnected;
        [ObservableProperty]
        private string currentUserDisplayName = "未登录";
        [ObservableProperty]
        private bool isAdmin;
        [ObservableProperty]
        private LightState indicatorState= LightState.Off;


        private readonly IServiceProvider _serviceProvider;// 用于解析其他ViewModel的依赖
        private readonly DispatcherTimer _timer;
        
        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            PlcService.ConnectionChanged += (x, connected) => IsPlcConnected = connected;
            PlcService.DataReceived += PlcService_DataReceived;

            var dashboard = _serviceProvider.GetRequiredService<DashBoardViewModel>();
            dashboard.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DashBoardViewModel.IndicatorState))
                    IndicatorState = dashboard.IndicatorState;
            };

            UserService.LoginStateChanged += user =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateUser(user);

                }
             );
            };
            UpdateUser(UserService.CurrentUser);
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
            MainContent = _serviceProvider.GetRequiredService<DashBoardViewModel>();
        }
        private void UpdateUser(User user)
        {
            if (user == null)
            {
                CurrentUserDisplayName = "未登录";
                IsUserLoggedIn = false;
                IsAdmin = false;
            }
            else
            {
                CurrentUserDisplayName= user.UserName;
                IsUserLoggedIn = true;
                IsAdmin = user.Role==Role.Admin;
            }
        }
        [RelayCommand]
        private void Login()
        {
            var loginWin = new LoginWindow()
            {
                Owner = Application.Current.MainWindow

            };
            loginWin.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var result = loginWin.ShowDialog();
            UpdateUser(UserService.CurrentUser);
        }
        [RelayCommand]
        private void ExecuteExit()
        {
            var result = System.Windows.MessageBox.Show("确定要退出系统吗？","退出确认",MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }
        private void PlcService_DataReceived(object? sender, DeviceStates e)
        {
            CurrentBatchNo = e.BarCode; 
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
