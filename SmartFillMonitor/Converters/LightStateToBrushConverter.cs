using SmartFillMonitor.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
namespace SmartFillMonitor.Converters
{
    public class LightStateToBrushConverter : IValueConverter
    {
        private static readonly Color OffColor = Colors.DimGray;
        private static readonly Color GreenColor = Colors.Green;
        private static readonly Color YellowColor = Colors.Yellow;
        private static readonly Color RedColor = Colors.Red;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
           var state=value is LightState Is ? Is :LightState.Off; 
            var role = (parameter as string)?.ToLowerInvariant()??string.Empty;
            if (state == LightState.Off) return OffColor;
            return role switch
            {
                "green" => state == LightState.Green ? GreenColor : OffColor,
                "yellow" => state == LightState.Yellow ? YellowColor : OffColor,
                "red" => state == LightState.Red ? RedColor : OffColor,
                _=> OffColor
            };


        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
