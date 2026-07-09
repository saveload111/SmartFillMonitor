using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Logs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace SmartFillMonitor.ViewModels

{
    public partial class SettingViewModel : ObservableObject
    {

        // === 新增：连接模式选项 ===
        public ObservableCollection<ConnectionMode> ConnectionModeOptions { get; } = new()
          {
              ConnectionMode.Serial,
              ConnectionMode.ModbusTcp,
              ConnectionMode.CustomTcp
          };


        public ObservableCollection<string> PortNamesOptions { get; } = new ObservableCollection<string>();
        public ObservableCollection<int> BaudRatesOptions { get; } = new ObservableCollection<int>()
        {
            9600, 19200, 38400, 57600, 115200
        };
        public ObservableCollection<int> DataBitsOptions { get; } = new ObservableCollection<int>()
        {
            7, 8
        };
        public ObservableCollection<string> StopBitsOptions { get; } = new ObservableCollection<string>()
        {
            "None", "One", "Two"
        };

        public ObservableCollection<string> ParityBitsOptions { get; } = new ObservableCollection<string>()
        {
            "None", "Odd", "Even"
        };

        [ObservableProperty]
        private string selectedPortName = "COM3";

        [ObservableProperty]
        private int selectedBaudRate = 115200;

        [ObservableProperty]
        private int selectedDataBit = 8;

        [ObservableProperty]
        private string selectedStopBit = "One";

        [ObservableProperty]
        private string selectedParity = "None";

        [ObservableProperty]
        private bool autoConnect = true;

        [ObservableProperty]
        private bool alarmSound = true;

        [ObservableProperty]
        private bool debugLogMode = false;



        [ObservableProperty]  // === 新增：模式 + TCP 配置 ===
        private ConnectionMode selectedMode = ConnectionMode.Serial;

        [ObservableProperty]
        private string tcpIp = "127.0.0.1";

        [ObservableProperty]
        private int tcpPort = 502;

        // === 新增：用于 XAML 面板显隐 ===
        public bool IsSerialMode => SelectedMode == ConnectionMode.Serial;
        public bool IsTcpMode => SelectedMode != ConnectionMode.Serial;

        // SelectedMode 变化时刷新显隐属性
        partial void OnSelectedModeChanged(ConnectionMode value)
        {
            OnPropertyChanged(nameof(IsSerialMode));
            OnPropertyChanged(nameof(IsTcpMode));
        }





        [RelayCommand]
        private async Task TestConnectionAsync()
        {
            // 先保存当前设置
            await SaveAsync();
            // 再用当前连接测试
            if (!PlcService.IsConnected)
            {
                System.Windows.MessageBox.Show("当前未连接 PLC，请先设置参数并启动自动连接", "测试连接", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            try
            {
                var state = await PlcService.ReadStateAsync();
                System.Windows.MessageBox.Show(
                    $"连接成功！PLC 设备响应正常\n产量: {state.ActualCount}\n温度: {state.CurrentTemp}℃\n液位: {state.LiquidLevel}",
                    "测试连接",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"连接失败：{ex.Message}", "测试连接", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                var model = new Models.DeviceSettings
                {
                    // === 新增 ===
                    Mode = SelectedMode,
                    TcpIp = TcpIp,
                    TcpPort = TcpPort,
                    PortName = selectedPortName,
                    BaudRate = selectedBaudRate,
                    DataBit = selectedDataBit,
                    StopBit = selectedStopBit,
                    Parity = selectedParity,
                    AutoConnect = autoConnect,
                    AlarmSound = alarmSound,
                    DebugLogMode = debugLogMode
                };
                await Services.ConfigServices.SaveDeviceSettingsAsync(model);
                // 立即应用新设置，无需重启
                await PlcService.Initialize(model);
                LogService.Info($"设置已保存并应用，模式: {model.Mode}");
            }

            catch (Exception ex)
            {
                LogService.Error($"保存配置文件失败，原因:{ex.Message}");
            }
        }
        public SettingViewModel()
        {

            RefreshPortList();
            try
            {
                LoadSettings();
            }


            catch (Exception ex)
            { // Handle the exception, e.g., log it or show a message to the user
                LogService.Error($"加载配置文件失败，使用默认值，原因:{ex.Message}");
            }


        }
        private async void LoadSettings()
        {
            var ds = await Services.ConfigServices.LoadDeviceSettingsAsync();

            // === 新增 ===
            SelectedMode = ds.Mode;
            TcpIp = ds.TcpIp;
            TcpPort = ds.TcpPort;

            SelectedPortName = ds.PortName;
            SelectedBaudRate = ds.BaudRate;
            SelectedDataBit = ds.DataBit;
            SelectedStopBit = string.IsNullOrEmpty(ds.StopBit) ? "One" : ds.StopBit;
            SelectedParity = ds.Parity;
            AutoConnect = ds.AutoConnect;
            AlarmSound = ds.AlarmSound;
            DebugLogMode = ds.DebugLogMode;
        }
        private void RefreshPortList()
        {
            PortNamesOptions.Clear();
            try
            {
                var ports = PlcService.GetAvailablePorts()??SerialPort.GetPortNames();
                foreach (var item in ports)
                {  PortNamesOptions.Add(item); }
                if (!string.IsNullOrEmpty(SelectedPortName)&&!PortNamesOptions.Contains(SelectedPortName))
                {
                    SelectedPortName = PortNamesOptions.Count > 0 ? PortNamesOptions[0] : SelectedPortName;
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"获取串口列表失败，原因:{ex.Message}");
                PortNamesOptions.Clear();
                PortNamesOptions.Add("COM1");
                PortNamesOptions.Add("COM2");


            }
            
        }
    }
}