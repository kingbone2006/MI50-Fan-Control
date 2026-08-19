using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MI50FanControl.Services;
using WpfColor = System.Windows.Media.Color;
using WpfBinding = System.Windows.Data.Binding;

namespace MI50FanControl.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool flag && flag;
            bool shouldInvert = Invert || (parameter is string pStr && pStr.Equals("invert", StringComparison.OrdinalIgnoreCase));
            if (shouldInvert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility vis && vis == Visibility.Visible;
        }
    }

    public class TempToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            float temp = 0;
            if (value is float f) temp = f;
            else if (value is double d) temp = (float)d;
            else if (value is int i) temp = i;

            if (temp >= 85) return new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)); // Red
            if (temp >= 70) return new SolidColorBrush(WpfColor.FromRgb(249, 115, 22)); // Orange
            if (temp >= 55) return new SolidColorBrush(WpfColor.FromRgb(234, 179, 8)); // Yellow
            return new SolidColorBrush(WpfColor.FromRgb(0, 210, 255)); // Ice Cyan
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Success => new SolidColorBrush(WpfColor.FromRgb(16, 185, 129)),  // Green
                    LogLevel.Warning => new SolidColorBrush(WpfColor.FromRgb(245, 158, 11)),  // Amber
                    LogLevel.Error => new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)),    // Red
                    LogLevel.Hardware => new SolidColorBrush(WpfColor.FromRgb(0, 210, 255)),  // Cyan
                    LogLevel.Debug => new SolidColorBrush(WpfColor.FromRgb(139, 148, 158)),   // Gray
                    _ => new SolidColorBrush(WpfColor.FromRgb(240, 246, 252))                 // White
                };
            }
            return new SolidColorBrush(WpfColor.FromRgb(240, 246, 252));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TabHighlightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is string current && values[1] is string target)
            {
                return string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RadioBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Equals(value, parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? parameter : WpfBinding.DoNothing;
        }
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is bool b && b);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is bool b && b);
        }
    }
}
