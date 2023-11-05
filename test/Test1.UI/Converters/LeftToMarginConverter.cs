using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Test1.Converters
{
    public class LeftToMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is TimeSpan s && values[1] is int scale)
            {
                return new Thickness(s.TotalSeconds * scale, 0, 0, 0);
            }
            return new Thickness(0, 0, 0, 0);
        }


        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
