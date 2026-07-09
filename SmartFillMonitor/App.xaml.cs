using Microsoft.Extensions.DependencyInjection;

using Serilog;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Logs;
using SmartFillMonitor.ViewModels;
using SmartFillMonitor.Views;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace SmartFillMonitor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {//定义一个静态的RichTextBox控件，用于显示日志信息，设置为只读，并配置滚动条和样式
        public static RichTextBox LogView = new RichTextBox()
        {
            IsReadOnly = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = System.Windows.Media.Brushes.Black,
            Foreground = System.Windows.Media.Brushes.White,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
        };
        private const string LogTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss fff} {Level}]({ThreadId}) {Message:lj}{NewLine}{Exception}";
        private const string LogPath = "logs\\log-.txt";
        private const string DbFilePath = "SmartFillMonitor.db";
        private const string DbConnectionString = "Data Source=SmartFillMonitor.db";
        public IServiceProvider ServiceProvider { get; private set; }// 保存已经构建的DI服务，让其他类可以解析到依赖
        protected override async void OnStartup(StartupEventArgs e)
        {
           
            base.OnStartup(e);
            SetExceptionHandling();
            // 先初始化数据库（建表），再启动 Serilog 日志，避免写入冲突
            await InitializeCoreService();

            Configlogging();// 配置日志服务

            try
            {
                var services = new ServiceCollection();
                ConfigureServices(services);
                ServiceProvider = services.BuildServiceProvider();
                await InitialLoginFolowAsync();
                LogService.Debug("Initalizing PLC Service...");
                try
                {
                    var plcSettings = await ConfigServices.LoadDeviceSettingsAsync();
                    // 启动时检查串口是否存在
                    if (plcSettings is { AutoConnect: true, Mode: ConnectionMode.Serial })
                    {
                        var ports = System.IO.Ports.SerialPort.GetPortNames();
                        if (!ports.Contains(plcSettings.PortName))
                        {
                            LogService.Warn($"配置的串口 {plcSettings.PortName} 不存在，可用串口: {string.Join(", ", ports)}");
                            MessageBox.Show(
                                $"配置的串口 \"{plcSettings.PortName}\" 不存在。\n\n可用串口: {(ports.Length > 0 ? string.Join(", ", ports) : "无")}\n\n请前往设置页面修改串口号。",
                                "串口不可用",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                    await PlcService.Initialize(plcSettings);
                }
                catch (Exception ex)
                {
                    LogService.Warn($"PLC Service Initialization failed: {ex.Message}");
                }  
                LogService.Info("Core Services Initialized successfully");

            }
            catch (Exception ex)
            {
                LogService.Fatal("应用程序启动失败：{0}", ex);
                MessageBox.Show($"应用程序启动失败:{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(-1);
            }


        }
        protected override async void OnExit(ExitEventArgs e)
        {
            await PlcService.DisConnectAsync();
            Log.CloseAndFlush();//确保log缓存日志写入文件
            base.OnExit(e);
        }

        private async Task InitializeCoreService()
        {
            Log.Debug("Initialize Database......");
            DbProvider.Initialize(DbConnectionString);
            await UserService.InitializeAsync();//确保数据库结构存在
           
        }


        private async Task InitialLoginFolowAsync()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var LoginWindow = new LoginWindow
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen


            };
            bool? result = LoginWindow.ShowDialog();
            if (result == true)
            {
                LogService.Info("登录成功，启动主窗口");
                var mainVM = ServiceProvider.GetRequiredService<MainWindowViewModel>();
                var mainWindow = new MainWindow
                {
                    DataContext = mainVM,
                };
                Current.MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
            }
            else
            {
                Shutdown();
            }
        }


        private void Configlogging()
        {
            // Configure logging services here
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithThreadId()
                .WriteTo.RichTextBox(LogView, outputTemplate: LogTemplate)
                .WriteTo.Console(outputTemplate: LogTemplate)
                .WriteTo.File(LogPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31, outputTemplate: LogTemplate, shared: true)
                .WriteTo.SQLite(DbFilePath, tableName: "SystemLog", storeTimestampInUtc: false)
                .CreateLogger();



        }

        #region DI
        private void ConfigureServices(IServiceCollection services)
        {
            // Register your services and view models here
            // Example:
            services.AddSingleton<AlarmsViewModel>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<DashBoardViewModel>();
            services.AddSingleton<DashQueryViewModel>();
            services.AddSingleton<LogsViewModel>();
            services.AddSingleton<SettingViewModel>();
            services.AddTransient<LoginViewModel>();
        }
        #endregion


        private void SetExceptionHandling()
        {
            //UI线程捕获异常
            DispatcherUnhandledException += (s, e) =>
            {
                LogService.Error("UI线程未处理异常：{0}", e.Exception);
                e.Handled = true;
                MessageBox.Show($"UI异常{e.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            //非UI线程捕获异常
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                LogService.Fatal( "非UI线程未处理异常");
            
             
            };
            //Task内部捕获异常
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                var ex = e.Exception?.InnerException ?? e.Exception;
                LogService.Error($"Task.UnobservedTaskException: {ex?.Message}", ex);
                e.SetObserved();//标记为已处理
            };
    }

      
    }
}
