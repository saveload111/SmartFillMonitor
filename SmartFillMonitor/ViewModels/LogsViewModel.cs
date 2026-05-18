using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFillMonitor.Services.Logs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFillMonitor.ViewModels
{
    public partial class LogsViewModel : ObservableObject
    {
        public LogsViewModel() 
        {
          _ =  LoadLogsAsync();


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



        }
        [RelayCommand]
        private async Task ExportAsync()
        {


        }

        [RelayCommand]
        private async Task PreviousPageAsync()
        {


        }
        [RelayCommand]
        private async Task NextPageAsync()
        {


        }

        private async Task LoadLogsAsync()
        {
            if (IsBusy) return;
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
            var start = StartDate.ToString("yyyy-MM-dd ");
            var end = EndDate.ToString("yyyy-MM-dd ");
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