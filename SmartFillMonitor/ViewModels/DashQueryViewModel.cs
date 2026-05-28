using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Logs;
namespace SmartFillMonitor.ViewModels
{
    public partial class DashQueryViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<ProductionRecord> records = new();

        [ObservableProperty]
        private ProductionRecord? selectedRecord;

        [ObservableProperty]
        private DateTime? startDate = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime? endDate = DateTime.Today;





        public DashQueryViewModel()
        {



        }
        [RelayCommand]
        private async Task QueryAsync()
        {
            var start = StartDate??DateTime.Today.AddDays(-7);
            var end = EndDate ?? DateTime.Today;
            var endInclusive = end.AddDays(1).AddMicroseconds(-1);
            var list = await DataService.QueryRecordAsync(start,endInclusive);
            Records.Clear();
            foreach (var r in list.OrderByDescending(r => r.Time))
                {
                Records.Add(r);

            }
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            if (Records == null || Records.Count == 0) return;

            var dlg = new SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                FileName = $"ProductionRecords_{DateTime.Now:yyyyMMdd_HHmmss}.csv"


            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                await DataService.ExportToCsvAsync(Records.ToList(), dlg.FileName);
            }
            catch (Exception ex)
            {

               LogService.Error("导出生产记录失败",ex);
            }
            {
               




            }   



        }


    }
}