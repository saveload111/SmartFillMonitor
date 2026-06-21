using Microsoft.Extensions.DependencyInjection;
using SmartFillMonitor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SmartFillMonitor.Views
{
    /// <summary>
    /// DashBoardView.xaml 的交互逻辑
    /// </summary>
    public partial class DashBoardView : UserControl
    {
        public DashBoardView()
        {
            InitializeComponent();
            // 修复 X 轴标签浮点精度问题（如 0.6 显示为 0.600000000001）
            TempChart.AxisX[0].LabelFormatter = val => Math.Abs(val % 1) < 1e-10
                ? val.ToString("F0")
                : val.ToString("F1");
            var app = Application.Current as App;
            if (app.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetRequiredService<DashBoardViewModel>();
            }
        }

       
    }
}
