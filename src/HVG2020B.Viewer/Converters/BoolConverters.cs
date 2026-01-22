using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HVG2020B.Viewer.Converters;

/// <summary>
/// Converts boolean to its inverse.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return value;
    }
}

/// <summary>
/// Shows element only when RS232 mode is selected.
/// </summary>
public class Rs232VisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string mode && mode == "RS232")
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
