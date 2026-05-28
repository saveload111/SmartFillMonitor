using Microsoft.Extensions.DependencyInjection;
using Serilog;
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
        private const string DbConnectionString = "Data Source=SmartFillMonitor.db";//SQLite数据库的连接字符串，指定数据库文件的位置和名称,给FreeSql使用
        public IServiceProvider ServiceProvider { get; private set; }// 保存已经构建的DI服务，让其他类可以解析到依赖
        protected override async void OnStartup(StartupEventArgs e)
        {
           
            base.OnStartup(e);
            SetExceptionHandling();
            Configlogging();// 配置日志服务

            try
            {
                var services = new ServiceCollection();// Create a new DI service collection，依赖注入的第一步
                ConfigureServices(services);
                ServiceProvider = services.BuildServiceProvider();   //供外部调用
                await InitializeCoreService();// 初始化核心服务，如数据库连接等
                await InitialLoginFolowAsync();

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
            LogService.Debug("Initalizing PLC Service...");
            var plcSettings = await ConfigServices.LoadDeviceSettingsAsync();
            await PlcService.Initialize(plcSettings);
            LogService.Info("Core Services Initialized successfully");
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
                .MinimumLevel.Debug()
                .Enrich.WithThreadId()
                .WriteTo.RichTextBox(LogView, outputTemplate: LogTemplate)
                .WriteTo.Console(outputTemplate: LogTemplate)
                .WriteTo.File(LogPath, rollingInterval: RollingInterval.Day, outputTemplate: LogTemplate, shared: true)
                .WriteTo.SQLite(DbFilePath, tableName: "SystemLog", storeTimestampInUtc: false)
                .CreateLogger();



        }

        #region DI
        private void ConfigureServices(IServiceCollection services)
        {
            // Register your services and view models here
            // Example:
            services.AddSingleton<AlarmsViewModel>();
            services.AddTransient<MainWindowViewModel>();
            services.AddSingleton<DashBoardViewModel>();
            services.AddTransient<DashQueryViewModel>();
            services.AddSingleton<LogsViewModel>();
            services.AddTransient<SettingViewModel>();
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
                LogService.Error("Task.UnobservedTaskException");
                e.SetObserved();//标记为已处理
            };
    }

      
    }
}
