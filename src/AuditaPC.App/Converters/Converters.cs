using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AuditaPC.Core.Model;

namespace AuditaPC.App.Converters;

public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value is CheckStatus check ? check : CheckStatus.NotVerifiable;
        return status switch
        {
            CheckStatus.Verified => Brush("#166534"),
            CheckStatus.Detected => Brush("#1E4E8C"),
            CheckStatus.NotDetected => Brush("#55606D"),
            CheckStatus.Problem => Brush("#A62A20"),
            CheckStatus.Partial => Brush("#9A5B06"),
            CheckStatus.NotVerifiable => Brush("#5B3A86"),
            CheckStatus.RequiresElevation => Brush("#8A5A00"),
            CheckStatus.PermissionDenied => Brush("#A62A20"),
            CheckStatus.NotTestedOffline => Brush("#496579"),
            CheckStatus.KnownLimitation => Brush("#65717D"),
            CheckStatus.NotApplicable => Brush("#7A8492"),
            CheckStatus.QueryError => Brush("#8A1C14"),
            CheckStatus.Timeout => Brush("#9A4A06"),
            _ => Brush("#5C6674")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

public sealed class StatusToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value is CheckStatus check ? check : CheckStatus.NotVerifiable;
        return status switch
        {
            CheckStatus.Verified => Brush("#E7F4EA"),
            CheckStatus.Detected => Brush("#E9EFF8"),
            CheckStatus.NotDetected => Brush("#EEF0F2"),
            CheckStatus.Problem => Brush("#FBE9E6"),
            CheckStatus.Partial => Brush("#FBF0D9"),
            CheckStatus.NotVerifiable => Brush("#EFE9F7"),
            CheckStatus.RequiresElevation => Brush("#FFF2D8"),
            CheckStatus.PermissionDenied => Brush("#FBE9E6"),
            CheckStatus.NotTestedOffline => Brush("#EAF0F4"),
            CheckStatus.KnownLimitation => Brush("#EEF0F2"),
            CheckStatus.NotApplicable => Brush("#EEF0F2"),
            CheckStatus.QueryError => Brush("#F7E2DF"),
            CheckStatus.Timeout => Brush("#FAE8D7"),
            _ => Brush("#EEF0F2")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || (value is string text && string.IsNullOrWhiteSpace(text))
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || (value is string text && string.IsNullOrWhiteSpace(text))
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && !flag;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && flag ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility visibility && visibility == Visibility.Visible;
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && flag ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value is int number ? number : 0;
        return count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
