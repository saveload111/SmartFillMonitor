using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SmartFillMonitor.ViewModels;
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
        public IServiceProvider ServiceProvider { get; private set; }// 保存已经构建的DI服务，让其他类可以解析到依赖
        protected  override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Configlogging();// 配置日志服务
            var services = new ServiceCollection();// Create a new DI service collection，依赖注入的第一步
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();   //供外部调用
           
        }
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }
        private void Configlogging()
        {
            // Configure logging services here
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithThreadId()
                .WriteTo.RichTextBox(LogView,outputTemplate: LogTemplate)
                .WriteTo.Console(outputTemplate: LogTemplate)
                .WriteTo.File(LogPath, rollingInterval: RollingInterval.Day, outputTemplate: LogTemplate,shared: true)
                .WriteTo.SQLite(DbFilePath,tableName:"SystemLog",storeTimestampInUtc:false)
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
    }

}
