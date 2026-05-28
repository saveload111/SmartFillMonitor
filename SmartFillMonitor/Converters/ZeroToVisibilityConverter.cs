using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SmartFillMonitor.Converters
{
    internal class ZeroToVisibilityConverter: IValueConverter//实现IValueConverter接口，用于将整数值转换为Visibility类型
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)//将绑定的值转换为Visibility类型
        {
            if (value is int Count)
            {
                return Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;//当Count为0时显示提示文本，否则隐藏提示文本
            }
            return System.Windows.Visibility.Collapsed;//如果绑定的值不是整数，默认隐藏提示文本
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
