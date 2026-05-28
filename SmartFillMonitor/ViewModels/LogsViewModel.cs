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

        private const int PageSize = 50; //每页显示的日志记录数
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
        private bool _isBusy; //指示数据库操作是否忙碌


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
            var query = BuildQuery();
            var allData = await query.OrderByDescending(x => x.Timestamp).ToListAsync();
            if (allData.Count == 0)
            {
                MessageBox.Show("没有找到符合条件的日志记录");
                return;
            }
                var lines = new List<string> { "时间,等级,内容,异常" };
                lines.AddRange(allData.Select(x => $"{x.Timestamp:yyyy-MM-dd HH:mm:ss},{x.Level},\"{ x.RenderedMessage.Replace("\"", "\"\"")}\",\"{x.Exception?.Replace("\n", "")}\""));//将日志记录转换为CSV格式的字符串，注意对消息内容中的双引号进行转义，并去除异常信息中的换行符
                var path = $"Logs_Export_{DateTime.Now:yyyyMMddHHmmss}.csv";
                await File.WriteAllLinesAsync(path, lines, Encoding.UTF8);
                MessageBox.Show($"日志已成功导出到{Path.GetFullPath(path)}");
            
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
                TotalCount =(int) await query.CountAsync();//获取满足条件的日志记录总数
                var data =await query.OrderByDescending(x => x.Timestamp).Page(PageIndex, PageSize).ToListAsync();//根据时间戳降序排序，并分页查询日志记录
                Logs = new ObservableCollection<Models.SystemLog>(data);//将查询结果转换为ObservableCollection，更新UI绑定的日志列表
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
            var query = Services.DbProvider.Fsql.Select<Models.SystemLog>();//从数据库中查询日志记录
            var start = StartDate.ToString("yyyy-MM-dd");
            var end = EndDate.ToString("yyyy-MM-dd");
            //Sqlite 存储的日期格式为IOSO8601字符串，比如2026-05-17T21:05:27.272  查询时需要使用date函数进行转换
            query = query.Where($"date(\"Timestamp\") >= '{start}' AND date(\"Timestamp\") <= '{end}'");
            if(!string.IsNullOrEmpty(SearchText))
            { 
              query = query.Where(x => x.RenderedMessage.Contains(SearchText));//根据搜索文本过滤日志记录

            }
            if(SelectedLevel != "All")
            {
                query = query.Where(x => x.Level.Contains(SelectedLevel));//根据日志级别过滤日志记录
            }
            return query;
        }
    }
}