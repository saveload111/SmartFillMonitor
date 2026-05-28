using Microsoft.Extensions.DependencyInjection;
using SmartFillMonitor.Services;
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
using System.Windows.Shapes;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services.Logs;

namespace SmartFillMonitor.Views
{
    /// <summary>
    /// LoginWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                await LoadUserAsync();
                if (PasswordBox != null)
                {
                    PasswordBox.Focus();//设置焦点到密码框
                }
            };

            KeyDown += LoginWindow_KeyDown;
            var app = Application.Current as App;
            if (app.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetRequiredService<LoginViewModel>();
            }
        }

        private void LoginWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                Login_Click(this, new RoutedEventArgs());
            }else if (e.Key == Key.Escape)
            {
                Cancel_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }


        private async Task LoadUserAsync()
        {
            try
            {
                List<User> users = await UserService.GetAllUsersAsync();
                UserNameCombo.ItemsSource = users;
                if (users != null && users.Count > 0)
                {
                    UserNameCombo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                LogService.Error("加载用户列表失败", ex);
            }
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();


            }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            var username = (UserNameCombo.SelectedItem as User)?.UserName ?? string.Empty;
            var password = PasswordBox.Password ?? string.Empty;
            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("请输入用户名", "提升", MessageBoxButton.OK, MessageBoxImage.Information);
                UserNameCombo.Focus();
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("请输入密码", "提升", MessageBoxButton.OK, MessageBoxImage.Information);
                PasswordBox.Focus();
                return;
            }
            IsEnabled = false;//简单锁定UI，防止重复点击操作
            try
            {
                var ok = await UserService.AuthenticateAsync(username, password);
                if (ok)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("用户名或密码错误", "登录失败", MessageBoxButton.OK, MessageBoxImage.Information);
                    PasswordBox.Clear();
                    PasswordBox.Focus();

                }

            }
            finally { IsEnabled = true; }
        }
    }
}
