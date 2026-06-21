using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFillMonitor.Services.Logs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SmartFillMonitor.ViewModels
{
    public partial class LogsViewModel : ObservableObject
    {
        public LogsViewModel() 
        {
           _=LoadLogsAsync();

        }



        [ObservableProperty]
        private DateTime _startDate = DateTime.Today;

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today.AddDays(1).AddSeconds(-1);

        [ObservableProperty]
        private string _selectedLevel = "All";

        private const int PageSize = 50;
        public ObservableCollection<string> LogLevels { get; } = new ObservableCollection<string>
      {

            "All",

            "Debug",
            "Information",
            "Warning",
            "Error",

      };
        [ObservableProperty]
        private string _searchText = "";

        [ObservableProperty]
        private ObservableCollection<Models.SystemLog> _logs = new ObservableCollection<Models.SystemLog>();

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _pageIndex = 1;

        [RelayCommand]
        private async Task SearchAsync()
        {
    
            PageIndex = 1;
            await LoadLogsAsync();
        }
        [RelayCommand]
        private async Task ExportAsync()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                FileName = $"Logs_Export_{DateTime.Now:yyyyMMddHHmmss}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            IsBusy = true;
            try
            {
                var query = BuildQuery();
                var total = (int)await query.CountAsync();
                if (total == 0)
                {
                    MessageBox.Show("没有找到符合条件的日志记录");
                    return;
                }

                const int batchSize = 1000;
                int page = 0;

                await using var writer = new StreamWriter(dlg.FileName, false, Encoding.UTF8);
                var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
                {
                    Delimiter = ";",
                };
                using var csv = new CsvHelper.CsvWriter(writer, config);

                csv.WriteField("时间");
                csv.WriteField("等级");
                csv.WriteField("内容");
                csv.WriteField("异常");
                await csv.NextRecordAsync();

                while (true)
                {
                    var batch = await query.OrderByDescending(x => x.Timestamp)
                        .Skip(page * batchSize).Take(batchSize).ToListAsync();
                    if (batch.Count == 0) break;

                    foreach (var log in batch)
                    {
                        csv.WriteField(log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                        csv.WriteField(log.Level);
                        csv.WriteField(log.RenderedMessage ?? "");
                        csv.WriteField(log.Exception?.Replace("\n", " ").Replace("\r", "") ?? "");
                        await csv.NextRecordAsync();
                    }
                    page++;
                    if (batch.Count < batchSize) break;
                }

                MessageBox.Show($"导出成功：{total} 条记录 → {Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex)
            {
                LogService.Error("导出日志失败", ex);
                MessageBox.Show($"导出失败：{ex.Message}");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task PreviousPageAsync()
        {
            
            
            if(PageIndex > 1)
            {
                PageIndex--;
                await LoadLogsAsync();
            }
            else
            {
               
                return;
            }
        }
        [RelayCommand]
        private async Task NextPageAsync()
        {
            if (PageIndex * PageSize < TotalCount)
            {
                PageIndex++;
                await LoadLogsAsync();
            }
            else
            {
               
                return;
            }
        }

        private async Task LoadLogsAsync()
        {
            if (IsBusy)
            {
                LogService.Warn("LoadLogsAsync 被跳过，正在忙碌中");
                return;
            }
            IsBusy = true;
            try
            {
       
                var query = BuildQuery();
                TotalCount =(int) await query.CountAsync();
                var data =await query.OrderByDescending(x => x.Timestamp).Page(PageIndex, PageSize).ToListAsync();
                Logs = new ObservableCollection<Models.SystemLog>(data);
            }
                                         
            catch (Exception ex)
            {

                LogService.Error( "加载日志失败",ex);
            }

            finally
            {
                IsBusy = false;


            }


        }
        private FreeSql.ISelect<Models.SystemLog> BuildQuery()
        {
            var query = Services.DbProvider.Fsql.Select<Models.SystemLog>();
            var start = StartDate.ToString("yyyy-MM-dd");
            var end = EndDate.ToString("yyyy-MM-dd");
            query = query.Where($"date(\"Timestamp\") >= '{start}' AND date(\"Timestamp\") <= '{end}'");
            if(!string.IsNullOrEmpty(SearchText))
            { 
              query = query.Where(x => x.RenderedMessage.Contains(SearchText));

            }
            if(SelectedLevel != "All")
            {
                query = query.Where(x => x.Level.Contains(SelectedLevel));
            }
            return query;
        }
    }
}
