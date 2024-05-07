using Avalonia.Data.Converters;
using FlowMaker.ViewModels;
using System;
using System.Globalization;
using System.Windows;

namespace FlowMaker.Converters
{
    public class EqualToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.Equals(parameter);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isChecked = (bool)value;
            if (!isChecked)
            {
                return null;
            }

            return parameter;
        }
    }

    public class EqualToVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value.Equals(parameter) ? true : false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return parameter;
        }
    }
    public class CountToVisibilityReConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (int.TryParse(parameter?.ToString(), out var count) && value is int vv)
            {
                return !vv.Equals(count) ? true : false;
            }
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return parameter;
        }
    }
    public class StartWithToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null || parameter is null)
            {
                return false;
            }
            return value.ToString()!.StartsWith(parameter.ToString()!) ? true : false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return parameter;
        }
    }


    public class SpikeDeleteBoxConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values[0] is SpikeTabViewModel tab && values[1] is SpikeBoxViewModel box)
            {
                return (tab, box);
            }
            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class SpikeDeleteActionConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values[0] is SpikeBoxViewModel tab && values[1] is SpikeActionViewModel box)
            {
                return (tab, box);
            }
            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
