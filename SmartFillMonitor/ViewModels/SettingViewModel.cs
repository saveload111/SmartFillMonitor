using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Logs;
using System.IO.Ports;
namespace SmartFillMonitor.ViewModels

{
    public partial class SettingViewModel : ObservableObject
    {
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

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                var model = new Models.DeviceSettings
                {
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
            }

            catch (Exception ex)
            {
                LogService.Error($"保存配置文件失败，原因:{ex.Message}");
            }
        }
        public SettingViewModel()
        {

            RefreashPortList();
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
            SelectedPortName = ds.PortName;
            SelectedBaudRate = ds.BaudRate;
            SelectedDataBit = ds.DataBit;
            SelectedStopBit = string.IsNullOrEmpty(ds.StopBit) ? "One" : ds.StopBit;
            SelectedParity = ds.Parity;
            AutoConnect = ds.AutoConnect;
            AlarmSound = ds.AlarmSound;
            DebugLogMode = ds.DebugLogMode;
        }
        private void RefreashPortList()
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