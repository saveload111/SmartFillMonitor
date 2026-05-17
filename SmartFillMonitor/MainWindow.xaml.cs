using Microsoft.Extensions.DependencyInjection;
using SmartFillMonitor.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SmartFillMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var app = Application.Current as App;
            if (app.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetRequiredService<MainWindowViewModel>();
            }
        }
    }
}