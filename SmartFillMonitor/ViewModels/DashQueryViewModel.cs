using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Win32;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Logs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
            var start = StartDate ?? DateTime.Today.AddDays(-7);
            var end = EndDate ?? DateTime.Today;
            var endInclusive = end.AddDays(1).AddMicroseconds(-1);
            var list = await DataService.QueryRecordAsync(start, endInclusive);
            Records.Clear();
            foreach (var r in list.OrderByDescending(r => r.Time))
                Records.Add(r);
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                FileName = $"ProductionRecords_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var start = StartDate ?? DateTime.Today.AddDays(-7);
                var end = EndDate ?? DateTime.Today;
                var endInclusive = end.AddDays(1).AddMilliseconds(-1);
                var query = DbProvider.Fsql.Select<ProductionRecord>()
                    .Where(r => r.Time >= start && r.Time <= endInclusive)
                    .OrderByDescending(r => r.Time);

                var total = (int)await query.CountAsync();
                if (total == 0)
                {
                    MessageBox.Show("没有找到符合条件的生产记录");
                    return;
                }

                const int batchSize = 1000;
                int page = 0;

                await using var writer = new StreamWriter(dlg.FileName, false, Encoding.UTF8);
              
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ";",
                    HasHeaderRecord = true,
                };
                using var csv = new CsvWriter(writer, config);
                await writer.WriteLineAsync("时间,批次号,实际产量,目标产量,设定温度,实际温度,节拍,是否NG");
                while (true)
                {
                    var batch = await query.Skip(page * batchSize).Take(batchSize).ToListAsync();
                    if (batch.Count == 0) break;

                    foreach (var r in batch)
                        await writer.WriteLineAsync($"{r.Time:yyyy-MM-dd HH:mm:ss},{r.BatchNo},{r.ActualCount},{r.TargetCount},{r.SettingTemp:F1},{r.ActualTemp:F1},{r.CycleTime:F2},{r.IsNG}");

                    page++;
                    if (batch.Count < batchSize) break;
                }

                MessageBox.Show($"导出成功：{total} 条记录");
            }
            catch (Exception ex)
            {
                LogService.Error("导出生产记录失败", ex);
                MessageBox.Show($"导出失败：{ex.Message}");

            }
        }
    }
}
