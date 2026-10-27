using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using TaskExchange.Models;
using TaskExchange.Services;

namespace TaskExchange.UI
{
    public sealed class ManagerWindow : Window
    {
        private readonly UIApplication _application;
        private readonly TaskManagerSession _session;
        private readonly SettingsStore _store = new SettingsStore();
        private UserSettings _settings;
        private ObservableCollection<LinkSummary> _links;
        private readonly DataGrid _grid;
        private readonly TextBlock _status;
        private TasksWindow _tasksWindow;
        private SettingsWindow _settingsWindow;
        private bool _closing;

        internal ManagerWindow(UIApplication application, TaskManagerSession session, string familyName)
        {
            _application = application;
            _session = session;
            _settings = _store.Load();
            Title = "UNI — Менеджер заданий";
            Width = 1080;
            Height = 620;
            MinWidth = 850;
            MinHeight = 450;
            UiTheme.ApplyWindow(this);
            UiHelpers.SetRevitOwner(this, application);

            var root = new DockPanel { Margin = new Thickness(12) };
            Content = root;

            var header = new TextBlock
            {
                Text = "Менеджер заданий",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4, 2, 4, 10),
                Foreground = UiTheme.Text
            };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var footer = new DockPanel { LastChildFill = true, Margin = new Thickness(4, 7, 4, 3) };
            var footerCard = UiTheme.Card(footer, new Thickness(0, 8, 0, 0));
            DockPanel.SetDock(footerCard, Dock.Bottom);
            root.Children.Add(footerCard);

            var leftButtons = new StackPanel { Orientation = Orientation.Horizontal };
            var openButton = UiHelpers.Button("Задания", OpenTasks);
            UiTheme.StyleButton(openButton, UiTheme.ButtonTone.Primary);
            leftButtons.Children.Add(openButton);
            leftButtons.Children.Add(UiHelpers.Button("Обновить", Refresh));
            footer.Children.Add(leftButtons);

            var rightButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            rightButtons.Children.Add(UiHelpers.Button("Настройки", OpenSettings));
            rightButtons.Children.Add(UiHelpers.Button("Импорт", Import));
            rightButtons.Children.Add(UiHelpers.Button("Экспорт", Export));
            DockPanel.SetDock(rightButtons, Dock.Right);
            footer.Children.Add(rightButtons);

            _status = new TextBlock { Margin = new Thickness(10, 9, 10, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = UiTheme.Muted };
            footer.Children.Add(_status);

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
            };
            UiTheme.ApplyGrid(_grid);
            AddColumns();
            root.Children.Add(UiTheme.Card(_grid, new Thickness(0)));
            LoadLinks();
            Closing += (s, e) => _closing = true;
            Closed += (s, e) =>
            {
                _tasksWindow?.Close();
                _settingsWindow?.Close();
            };
        }

        private void AddColumns()
        {
            _grid.Columns.Add(TextColumn("Связь", "LinkName", 2));
            _grid.Columns.Add(TextColumn("Размещено", "Placed"));
            _grid.Columns.Add(TextColumn("Новые", "New"));
            _grid.Columns.Add(TextColumn("Перемещены", "Moved"));
            _grid.Columns.Add(TextColumn("Изменены параметры", "ParametersChanged", 1.3));
            _grid.Columns.Add(TextColumn("Удалены", "Deleted"));
            _grid.Columns.Add(TextColumn("У смежников", "AtPartners", 1.1));
            _grid.Columns.Add(TextColumn("Всего", "Total"));
            _grid.Columns.Add(new DataGridComboBoxColumn
            {
                Header = "Конфигурация",
                DisplayMemberPath = "Name",
                SelectedItemBinding = new Binding("Configuration") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(1.5, DataGridLengthUnitType.Star)
            });
        }

        private static DataGridTextColumn TextColumn(string header, string path, double width = 1) => new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(path),
            Width = new DataGridLength(width, DataGridLengthUnitType.Star),
            IsReadOnly = true
        };

        private void LoadLinks(IEnumerable<string> selectedLinkIds = null)
        {
            try
            {
                var selectedIds = selectedLinkIds == null
                    ? null
                    : new HashSet<string>(selectedLinkIds, StringComparer.Ordinal);
                var scanner = new AssignmentScanner(_application.ActiveUIDocument.Document, _settings);
                _links = new ObservableCollection<LinkSummary>(scanner.ScanLinks());
                _grid.ItemsSource = _links;
                DataGridComboBoxColumn combo = _grid.Columns.OfType<DataGridComboBoxColumn>().First();
                combo.ItemsSource = _settings.Configurations;
                _status.Text = _links.Count == 0
                    ? "В проекте нет загруженных связей."
                    : $"Связей: {_links.Count}. Выберите одну или несколько строк (Ctrl/Shift).";
                if (selectedIds != null)
                {
                    foreach (LinkSummary link in _links.Where(link => selectedIds.Contains(link.LinkUniqueId)))
                        _grid.SelectedItems.Add(link);
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Ошибка сканирования связей", ex);
                _status.Text = "Ошибка: " + ex.Message;
            }
        }

        private void SaveLinkConfigurations()
        {
            foreach (LinkSummary link in _links ?? Enumerable.Empty<LinkSummary>())
            {
                if (link.Configuration != null)
                    _settings.LinkConfigurationIds[link.LinkUniqueId] = link.Configuration.Id;
            }
            _store.Save(_settings);
        }

        private void OpenTasks(object sender, RoutedEventArgs e)
        {
            SaveLinkConfigurations();
            List<LinkSummary> selected = _grid.SelectedItems.Cast<LinkSummary>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Выберите одну или несколько связей.", "Обмен заданиями");
                return;
            }
            _session.Post(application =>
            {
                if (_tasksWindow != null)
                {
                    _tasksWindow.Activate();
                    return;
                }
                var selectedIds = new HashSet<string>(selected.Select(link => link.LinkUniqueId), StringComparer.Ordinal);
                var window = new TasksWindow(application, _session, _settings, selected);
                UiHelpers.SetRevitOwner(window, application);
                _tasksWindow = window;
                window.Closed += (s, args) =>
                {
                    _tasksWindow = null;
                    RestoreManager();
                    _session.Post(app => LoadLinks(selectedIds));
                };
                ShowChild(window);
            });
        }

        private void OpenSettings(object sender, RoutedEventArgs e)
        {
            SaveLinkConfigurations();
            var selectedIds = new HashSet<string>(
                _grid.SelectedItems.Cast<LinkSummary>().Select(link => link.LinkUniqueId),
                StringComparer.Ordinal);
            _session.Post(application =>
            {
                if (_settingsWindow != null)
                {
                    _settingsWindow.Activate();
                    return;
                }
                var window = new SettingsWindow(application, _settings);
                UiHelpers.SetRevitOwner(window, application);
                _settingsWindow = window;
                window.Closed += (s, args) =>
                {
                    _settingsWindow = null;
                    RestoreManager();
                    if (window.Saved)
                    {
                        _settings = _store.Load();
                        _session.Post(app => LoadLinks(selectedIds));
                    }
                };
                ShowChild(window);
            });
        }

        private void ShowChild(Window window)
        {
            try
            {
                Hide();
                window.Show();
            }
            catch
            {
                if (!_closing && !_session.IsClosed)
                    Show();
                throw;
            }
        }

        private void RestoreManager()
        {
            if (_closing || _session.IsClosed) return;
            Show();
            Activate();
        }

        private void Refresh(object sender, RoutedEventArgs e)
        {
            SaveLinkConfigurations();
            _session.Post(application => LoadLinks());
        }

        private void Import(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Конфигурация заданий (*.json)|*.json", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                _settings = _store.Import(dialog.FileName, _settings);
                _store.Save(_settings);
                _session.Post(application => LoadLinks());
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Ошибка импорта"); }
        }

        private void Export(object sender, RoutedEventArgs e)
        {
            SaveLinkConfigurations();
            var dialog = new SaveFileDialog { Filter = "Конфигурация заданий (*.json)|*.json", FileName = "TaskExchange.config.json" };
            if (dialog.ShowDialog(this) != true) return;
            try { _store.Export(dialog.FileName, _settings); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Ошибка экспорта"); }
        }
    }
}
