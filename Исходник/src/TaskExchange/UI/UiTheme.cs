using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace TaskExchange.UI
{
    internal static class UiTheme
    {
        internal enum ButtonTone { Neutral, Primary, Danger }

        public static readonly Brush Canvas = Brush(247, 248, 250);
        public static readonly Brush Surface = Brushes.White;
        public static readonly Brush Border = Brush(215, 219, 224);
        public static readonly Brush Text = Brush(42, 48, 55);
        public static readonly Brush Muted = Brush(105, 114, 124);
        public static readonly Brush Accent = Brush(50, 109, 153);
        public static readonly Brush Header = Brush(237, 239, 242);
        public static readonly Brush Received = Brush(164, 229, 157);
        public static readonly Brush Moved = Brush(255, 241, 160);
        public static readonly Brush Deleted = Brush(244, 154, 157);
        public static readonly Brush Parameters = Brush(224, 199, 239);
        public static readonly Brush OtherLink = Brush(151, 202, 241);
        public static readonly Brush Neutral = Brushes.White;

        public static SolidColorBrush Brush(byte r, byte g, byte b) =>
            new SolidColorBrush(Color.FromRgb(r, g, b));

        public static Border Card(UIElement content, Thickness margin)
        {
            return new Border
            {
                Background = Surface,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Margin = margin,
                Child = content
            };
        }

        public static void ApplyWindow(Window window)
        {
            window.Background = Canvas;
            window.Foreground = Text;
            window.FontFamily = new FontFamily("Segoe UI");
            window.FontSize = 12;
        }

        public static void ApplyGrid(DataGrid grid)
        {
            grid.Background = Surface;
            grid.BorderThickness = new Thickness(0);
            grid.HeadersVisibility = DataGridHeadersVisibility.Column;
            grid.ColumnHeaderHeight = 36;
            grid.RowHeight = 31;
            grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
            grid.HorizontalGridLinesBrush = Border;
            grid.VerticalGridLinesBrush = Brushes.Transparent;
            grid.CanUserResizeColumns = true;
            grid.CanUserSortColumns = true;
            grid.SelectionUnit = DataGridSelectionUnit.FullRow;

            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Header));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Border));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 0, 4, 0)));
            grid.ColumnHeaderStyle = headerStyle;

            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(7, 2, 4, 2)));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            var selected = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, Brush(64, 130, 175)));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            cellStyle.Triggers.Add(selected);
            grid.CellStyle = cellStyle;
        }

        public static Button Action(string text, RoutedEventHandler click, ButtonTone tone = ButtonTone.Neutral)
        {
            var button = UiHelpers.Button(text, click, 0);
            StyleButton(button, tone);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.Height = 34;
            button.Margin = new Thickness(0, 0, 0, 6);
            return button;
        }

        public static void StyleButton(Button button, ButtonTone tone = ButtonTone.Neutral)
        {
            Brush hover;
            Brush pressed;
            button.Cursor = Cursors.Hand;
            button.FontWeight = FontWeights.SemiBold;
            button.BorderThickness = new Thickness(1);
            button.Padding = new Thickness(12, 0, 12, 0);
            switch (tone)
            {
                case ButtonTone.Primary:
                    button.Background = Accent;
                    button.Foreground = Brushes.White;
                    button.BorderBrush = Accent;
                    hover = Brush(39, 92, 133);
                    pressed = Brush(29, 73, 108);
                    break;
                case ButtonTone.Danger:
                    button.Background = Brush(255, 247, 247);
                    button.Foreground = Brush(151, 49, 55);
                    button.BorderBrush = Brush(231, 184, 188);
                    hover = Brush(252, 231, 232);
                    pressed = Brush(248, 214, 217);
                    break;
                default:
                    button.Background = Surface;
                    button.Foreground = Text;
                    button.BorderBrush = Border;
                    hover = Brush(235, 244, 250);
                    pressed = Brush(219, 234, 244);
                    break;
            }

            var chrome = new FrameworkElementFactory(typeof(Border));
            chrome.Name = "Chrome";
            chrome.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(5));
            chrome.SetValue(System.Windows.Controls.Border.SnapsToDevicePixelsProperty, true);
            chrome.SetBinding(System.Windows.Controls.Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            chrome.SetBinding(System.Windows.Controls.Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            chrome.SetBinding(System.Windows.Controls.Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
            chrome.SetBinding(System.Windows.Controls.Border.PaddingProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            chrome.AppendChild(content);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = chrome };
            var mouseOver = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            mouseOver.Setters.Add(new Setter(System.Windows.Controls.Border.BackgroundProperty, hover, "Chrome"));
            mouseOver.Setters.Add(new Setter(System.Windows.Controls.Border.BorderBrushProperty, tone == ButtonTone.Neutral ? Accent : button.BorderBrush, "Chrome"));
            template.Triggers.Add(mouseOver);
            var down = new Trigger { Property = Button.IsPressedProperty, Value = true };
            down.Setters.Add(new Setter(System.Windows.Controls.Border.BackgroundProperty, pressed, "Chrome"));
            template.Triggers.Add(down);
            var focused = new Trigger { Property = Button.IsKeyboardFocusedProperty, Value = true };
            focused.Setters.Add(new Setter(System.Windows.Controls.Border.BorderBrushProperty, Accent, "Chrome"));
            focused.Setters.Add(new Setter(System.Windows.Controls.Border.BorderThicknessProperty, new Thickness(2), "Chrome"));
            template.Triggers.Add(focused);
            var disabled = new Trigger { Property = Button.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5, "Chrome"));
            template.Triggers.Add(disabled);
            button.Template = template;
        }

        public static FrameworkElement LegendItem(Brush color, string label)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 14, 2) };
            line.Children.Add(new Border
            {
                Width = 16, Height = 12,
                Background = color,
                BorderBrush = Brush(125, 132, 139),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });
            line.Children.Add(new TextBlock { Text = label, Foreground = Text, VerticalAlignment = VerticalAlignment.Center });
            return line;
        }

        public static CheckBox Toggle(string label, RoutedEventHandler changed)
        {
            var toggle = new CheckBox { Content = label, Height = 22, Margin = new Thickness(0, 2, 0, 2), Cursor = Cursors.Hand };
            var line = new FrameworkElementFactory(typeof(StackPanel));
            line.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            var track = new FrameworkElementFactory(typeof(Border));
            track.Name = "Track";
            track.SetValue(FrameworkElement.WidthProperty, 31.0);
            track.SetValue(FrameworkElement.HeightProperty, 17.0);
            track.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            track.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(9));
            track.SetValue(System.Windows.Controls.Border.BackgroundProperty, Brush(191, 198, 204));
            var thumb = new FrameworkElementFactory(typeof(Border));
            thumb.Name = "Thumb";
            thumb.SetValue(FrameworkElement.WidthProperty, 13.0);
            thumb.SetValue(FrameworkElement.HeightProperty, 13.0);
            thumb.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
            thumb.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            thumb.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(7));
            thumb.SetValue(System.Windows.Controls.Border.BackgroundProperty, Brushes.White);
            track.AppendChild(thumb);
            line.AppendChild(track);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.MarginProperty, new Thickness(7, 0, 0, 0));
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            line.AppendChild(content);
            var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = line };
            var on = new Trigger { Property = CheckBox.IsCheckedProperty, Value = true };
            on.Setters.Add(new Setter(System.Windows.Controls.Border.BackgroundProperty, Accent, "Track"));
            on.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right, "Thumb"));
            template.Triggers.Add(on);
            toggle.Template = template;
            toggle.Checked += changed;
            toggle.Unchecked += changed;
            return toggle;
        }
    }
}
