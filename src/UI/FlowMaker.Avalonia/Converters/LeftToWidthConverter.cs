﻿using Avalonia.Data.Converters;
using System.Globalization;

namespace FlowMaker.Converters
{
    public class LeftToWidthConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
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
