using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TaskExchange.UI
{
    internal sealed class ParameterPickerWindow : Window
    {
        private readonly ListBox _list;
        public List<SettingsWindow.FamilyParameterItem> SelectedItems { get; private set; } = new List<SettingsWindow.FamilyParameterItem>();

        public ParameterPickerWindow(List<SettingsWindow.FamilyParameterItem> parameters, bool shared)
        {
            Title = shared ? "Выберите общие параметры" : "Выберите встроенные параметры";
            Width = 440;
            Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            UiTheme.ApplyWindow(this);
            var root = new DockPanel { Margin = new Thickness(12) };
            Content = root;
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var add = UiHelpers.Button("Добавить", Accept, 100);
            UiTheme.StyleButton(add, UiTheme.ButtonTone.Primary);
            buttons.Children.Add(add);
            buttons.Children.Add(UiHelpers.Button("Отмена", (s, e) => Close(), 90));
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);
            var heading = new TextBlock
            {
                Text = shared ? "Общие параметры семейства задания" : "Встроенные параметры семейства задания",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(3, 2, 3, 10)
            };
            DockPanel.SetDock(heading, Dock.Top);
            root.Children.Add(heading);
            _list = new ListBox
            {
                ItemsSource = parameters,
                SelectionMode = SelectionMode.Extended,
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(8),
                BorderBrush = UiTheme.Border,
                Background = UiTheme.Surface
            };
            root.Children.Add(_list);
        }

        private void Accept(object sender, RoutedEventArgs e)
        {
            SelectedItems = _list.SelectedItems.Cast<SettingsWindow.FamilyParameterItem>().ToList();
            if (SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Выберите хотя бы один параметр.", "Обмен заданиями");
                return;
            }
            DialogResult = true;
        }
    }
}
