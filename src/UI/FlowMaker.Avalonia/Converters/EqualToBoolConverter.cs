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
}
