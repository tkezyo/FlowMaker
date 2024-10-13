using FlowMaker.ViewModels;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlowMaker.Converters;

public class EqualToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value.Equals(parameter);
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
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
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;
        return value.Equals(parameter) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return parameter;
    }
}

public class CountToVisibilityReConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return Visibility.Collapsed;
        }
        if (int.TryParse(parameter?.ToString(), out var count) && value is int vv)
        {
            return !vv.Equals(count) ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return parameter;
    }
}

public class SendEventVisibilityReConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var value = values[0];
        if (value is null)
        {
            return Visibility.Collapsed;
        }
        if (value is DataDisplayViewModel data && data.WaitEvents is not null)
        {
            return data.WaitEvents.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
public class StartWithToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return Visibility.Collapsed;
        }
        return value.ToString()!.StartsWith(parameter.ToString()!) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return parameter;
    }
}
