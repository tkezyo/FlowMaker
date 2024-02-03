using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlowMaker.Converters
{
    public class LeftToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is TimeSpan s && values[1] is int scale)
            {
                return s.TotalSeconds * scale;
            }
            return 0;
        }



        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
