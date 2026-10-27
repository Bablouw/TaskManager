using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using Autodesk.Revit.UI;
using TaskExchange.Models;
using TaskExchange.Services;

namespace TaskExchange.UI
{
    internal static class UiHelpers
    {
        public static void SetRevitOwner(Window window, UIApplication application)
        {
            new WindowInteropHelper(window).Owner = application.MainWindowHandle;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        public static System.Windows.Controls.Button Button(string text, RoutedEventHandler click, double minWidth = 110)
        {
            var button = new System.Windows.Controls.Button
            {
                Content = text,
                MinWidth = minWidth,
                Height = 32,
                Margin = new Thickness(4)
            };
            UiTheme.StyleButton(button);
            button.Click += click;
            return button;
        }
    }

    public sealed class ParameterDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            ParameterCodec.Display(value as string);
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
    }

    public sealed class StatusBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is AssignmentStatus status)) return Brushes.Transparent;
            switch (status)
            {
                case AssignmentStatus.Received: return UiTheme.Received;
                case AssignmentStatus.Moved: return UiTheme.Moved;
                case AssignmentStatus.ParametersChanged: return UiTheme.Parameters;
                case AssignmentStatus.Deleted: return UiTheme.Deleted;
                case AssignmentStatus.InOtherLink: return UiTheme.OtherLink;
                default: return UiTheme.Neutral;
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
