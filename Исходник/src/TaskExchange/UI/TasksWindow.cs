using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Autodesk.Revit.UI;
using TaskExchange.Models;
using TaskExchange.Services;

namespace TaskExchange.UI
{
    public sealed class TasksWindow : Window
    {
        private readonly UIApplication _application;
        private readonly TaskManagerSession _session;
        private readonly UserSettings _settings;
        private readonly List<LinkSummary> _links;
        private readonly DataGrid _grid;
        private readonly TextBlock _counter;
        private readonly Dictionary<AssignmentStatus, CheckBox> _statusToggles = new Dictionary<AssignmentStatus, CheckBox>();
        private CheckBox _hideLinkOnly;
        private System.Windows.Controls.TextBox _search;
        private string _appliedQuery = string.Empty;
        private TextBlock _projectId;
        private TextBlock _linkId;
        private TextBlock _loadedBy;
        private TextBlock _loadedAt;
        private TextBlock _updatedAt;
        private ObservableCollection<AssignmentRow> _rows;
        private ICollectionView _view;

        internal TasksWindow(UIApplication application, TaskManagerSession session, UserSettings settings, List<LinkSummary> links)
        {
            _application = application;
            _session = session;
            _settings = settings;
            _links = links;
            Title = "UNI — Задания на подключение";
            Width = 1320;
            Height = 720;
            MinWidth = 1000;
            MinHeight = 560;
            UiTheme.ApplyWindow(this);

            var root = new DockPanel { Margin = new Thickness(12) };
            Content = root;

            var header = new DockPanel { Margin = new Thickness(4, 0, 4, 10) };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            header.Children.Add(new TextBlock
            {
                Text = "Задания на подключение",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = UiTheme.Text
            });
            header.Children.Add(new TextBlock
            {
                Text = "  •  " + string.Join(", ", _links.Select(x => x.LinkName)),
                Foreground = UiTheme.Muted,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            var right = CreateRightPanel();
            DockPanel.SetDock(right, Dock.Right);
            root.Children.Add(right);

            var bottom = new DockPanel { Margin = new Thickness(0, 8, 10, 0), LastChildFill = true };
            var bottomCard = UiTheme.Card(bottom, new Thickness(0));
            DockPanel.SetDock(bottomCard, Dock.Bottom);
            root.Children.Add(bottomCard);
            var selectButtons = new StackPanel { Orientation = Orientation.Horizontal };
            selectButtons.Children.Add(UiHelpers.Button("Выбрать все", (s, e) => SetAll(true), 105));
            selectButtons.Children.Add(UiHelpers.Button("Отменить выбор", (s, e) => SetAll(false), 130));
            DockPanel.SetDock(selectButtons, Dock.Left);
            bottom.Children.Add(selectButtons);
            _counter = new TextBlock { Margin = new Thickness(12, 10, 10, 0), Foreground = UiTheme.Muted, TextAlignment = TextAlignment.Right };
            DockPanel.SetDock(_counter, Dock.Right);
            bottom.Children.Add(_counter);
            var legend = new WrapPanel { Margin = new Thickness(8, 6, 0, 4), VerticalAlignment = VerticalAlignment.Center };
            legend.Children.Add(UiTheme.LegendItem(UiTheme.Received, "Задание получено"));
            legend.Children.Add(UiTheme.LegendItem(UiTheme.Moved, "Изменены координаты"));
            legend.Children.Add(UiTheme.LegendItem(UiTheme.Deleted, "Задание удалено"));
            legend.Children.Add(UiTheme.LegendItem(UiTheme.Parameters, "Изменены параметры"));
            legend.Children.Add(UiTheme.LegendItem(UiTheme.OtherLink, "Есть в других связях"));
            legend.Children.Add(UiTheme.LegendItem(UiTheme.Neutral, "Не забрано"));
            bottom.Children.Add(legend);

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = DataGridSelectionMode.Extended,
                IsReadOnly = false,
                FrozenColumnCount = 2,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
            };
            UiTheme.ApplyGrid(_grid);
            _grid.SelectionChanged += GridSelectionChanged;
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty,
                new Binding("Status") { Converter = new StatusBrushConverter() }));
            _grid.RowStyle = rowStyle;
            root.Children.Add(UiTheme.Card(_grid, new Thickness(0, 0, 0, 0)));
            BuildColumns();
            LoadRows();
        }

        private FrameworkElement CreateRightPanel()
        {
            var panel = new StackPanel { Width = 260, Margin = new Thickness(10, 10, 18, 10) };
            panel.Children.Add(new TextBlock { Text = "СВЕДЕНИЯ О ЗАДАНИИ", FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Muted, Margin = new Thickness(0, 0, 0, 8) });
            var ids = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            ids.ColumnDefinitions.Add(new ColumnDefinition());
            ids.ColumnDefinitions.Add(new ColumnDefinition());
            var project = Detail("ID в проекте", out _projectId);
            var link = Detail("ID в связи", out _linkId);
            Grid.SetColumn(link, 1);
            ids.Children.Add(project);
            ids.Children.Add(link);
            panel.Children.Add(ids);
            panel.Children.Add(Detail("Пользователь загрузки", out _loadedBy));
            panel.Children.Add(Detail("Дата загрузки задания", out _loadedAt));
            panel.Children.Add(Detail("Дата обновления задания", out _updatedAt));
            panel.Children.Add(UiTheme.Action("История версий", ShowHistory));
            var partners = UiTheme.Action("Задание у смежников", (s, e) => { });
            partners.IsEnabled = false;
            partners.ToolTip = "Подсчёт заданий у смежников пока не реализован";
            panel.Children.Add(partners);

            panel.Children.Add(new Separator { Margin = new Thickness(0, 6, 0, 10) });
            var selection = new Grid();
            selection.ColumnDefinitions.Add(new ColumnDefinition());
            selection.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            var select = UiTheme.Action("Выделить в модели", SelectInModel);
            var find = UiTheme.Action("≡", FindCurrentSelection);
            find.ToolTip = "Найти в списке выбранное в модели задание";
            find.Margin = new Thickness(5, 0, 0, 6);
            Grid.SetColumn(find, 1);
            selection.Children.Add(select);
            selection.Children.Add(find);
            panel.Children.Add(selection);
            panel.Children.Add(UiTheme.Action("Показать на виде", ShowInModel));
            panel.Children.Add(new TextBlock { Text = "ФИЛЬТРЫ", FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Muted, Margin = new Thickness(0, 9, 0, 5) });
            panel.Children.Add(new TextBlock { Text = "По значениям из связи", Foreground = UiTheme.Muted, Margin = new Thickness(0, 0, 0, 4) });
            _search = new System.Windows.Controls.TextBox { Height = 29, Padding = new Thickness(5, 3, 5, 3), ToolTip = "Поиск по значениям передаваемых параметров" };
            panel.Children.Add(_search);
            var searchButtons = new Grid { Margin = new Thickness(0, 5, 0, 6) };
            searchButtons.ColumnDefinitions.Add(new ColumnDefinition());
            searchButtons.ColumnDefinitions.Add(new ColumnDefinition());
            var apply = UiTheme.Action("Применить", (s, e) => { _appliedQuery = _search.Text.Trim(); _view?.Refresh(); });
            var reset = UiTheme.Action("Сбросить", (s, e) => ResetFilters());
            reset.Margin = new Thickness(5, 0, 0, 6);
            Grid.SetColumn(reset, 1);
            searchButtons.Children.Add(apply);
            searchButtons.Children.Add(reset);
            panel.Children.Add(searchButtons);

            AddStatusToggle(panel, AssignmentStatus.New, "Новые");
            AddStatusToggle(panel, AssignmentStatus.Moved, "Перемещённые");
            AddStatusToggle(panel, AssignmentStatus.Deleted, "Удалённые");
            _hideLinkOnly = UiTheme.Toggle("Скрыть задания из связей", (s, e) => _view?.Refresh());
            _hideLinkOnly.ToolTip = "Скрыть задания, переданные из текущей модели и отображаемые в связях";
            panel.Children.Add(_hideLinkOnly);

            panel.Children.Add(new Separator { Margin = new Thickness(0, 9, 0, 10) });
            panel.Children.Add(UiTheme.Action("Добавить в проект / Обновить", AddOrUpdate, UiTheme.ButtonTone.Primary));
            panel.Children.Add(UiTheme.Action("Удалить", DeleteLocal, UiTheme.ButtonTone.Danger));
            var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Width = 306 };
            return UiTheme.Card(scroll, new Thickness(10, 0, 0, 0));
        }

        private static FrameworkElement Detail(string label, out TextBlock value)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 4, 5) };
            panel.Children.Add(new TextBlock { Text = label, Foreground = UiTheme.Muted, FontSize = 11 });
            value = new TextBlock { Text = "—", FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = label };
            panel.Children.Add(value);
            return panel;
        }

        private void AddStatusToggle(Panel panel, AssignmentStatus status, string label)
        {
            var toggle = UiTheme.Toggle(label, (s, e) => _view?.Refresh());
            _statusToggles.Add(status, toggle);
            panel.Children.Add(toggle);
        }

        private void ResetFilters()
        {
            _search.Text = string.Empty;
            _appliedQuery = string.Empty;
            foreach (CheckBox toggle in _statusToggles.Values) toggle.IsChecked = false;
            _hideLinkOnly.IsChecked = false;
            _view?.Refresh();
        }

        private void GridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _session.Post(app => MovePreviewService.Clear(app));
            foreach (AssignmentRow row in e.AddedItems.OfType<AssignmentRow>())
                row.IsSelected = true;
            UpdateDetails(_grid.SelectedItem as AssignmentRow);
        }

        private void UpdateDetails(AssignmentRow row)
        {
            _projectId.Text = row?.LocalElementId > 0 ? row.LocalElementId.ToString() : "—";
            _linkId.Text = row?.SourceElementId > 0 ? row.SourceElementId.ToString() : "—";
            _loadedBy.Text = string.IsNullOrWhiteSpace(row?.LoadedBy) ? "—" : row.LoadedBy;
            _loadedAt.Text = FormatDate(row?.LoadedAtUtc);
            _updatedAt.Text = FormatDate(row?.LastUpdatedAtUtc);
        }

        private static string FormatDate(string value) =>
            DateTime.TryParse(value, out DateTime date) ? date.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : "—";

        private void BuildColumns()
        {
            _grid.Columns.Clear();
            _grid.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "✓",
                Binding = new Binding("IsSelected") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = 35,
                IsReadOnly = false
            });
            _grid.Columns.Add(TextColumn("Связь", "LinkName", 155));
            _grid.Columns.Add(TextColumn("Семейство (в проекте)", "LocalFamilyName", 165));
            _grid.Columns.Add(TextColumn("Тип (в связи)", "TypeName", 145));

            var converter = new ParameterDisplayConverter();
            foreach (string parameterName in _links.SelectMany(x => x.Configuration?.Mappings ?? new List<ParameterMapping>())
                .Select(x => x.SourceParameter).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                _grid.Columns.Add(new DataGridTextColumn
                {
                    Header = parameterName,
                    Binding = new Binding($"Parameters[{parameterName}]") { Converter = converter },
                    Width = 140,
                    IsReadOnly = true
                });
            }
        }

        private static DataGridTextColumn TextColumn(string header, string path, double width) => new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(path),
            Width = width,
            IsReadOnly = true
        };

        private void LoadRows()
        {
            try
            {
                var scanner = new AssignmentScanner(_application.ActiveUIDocument.Document, _settings);
                _rows = new ObservableCollection<AssignmentRow>(scanner.ScanAssignments(_links.Select(x => x.LinkElementId)));
                foreach (AssignmentRow row in _rows) row.PropertyChanged += (s, e) => UpdateCounter();
                _view = CollectionViewSource.GetDefaultView(_rows);
                _view.Filter = FilterRow;
                _grid.ItemsSource = _view;
                UpdateDetails(_grid.SelectedItem as AssignmentRow);
                UpdateCounter();
            }
            catch (Exception ex)
            {
                LogService.Error("Ошибка чтения заданий", ex);
                MessageBox.Show(this, ex.Message, "Ошибка чтения заданий");
            }
        }

        private bool FilterRow(object item)
        {
            if (!(item is AssignmentRow row)) return true;
            var selected = _statusToggles.Where(x => x.Value.IsChecked == true).Select(x => x.Key).ToList();
            if (selected.Count > 0 && !selected.Contains(row.Status)) return false;
            if (_hideLinkOnly?.IsChecked == true && row.IsFromCurrentModel) return false;
            string query = _appliedQuery;
            if (string.IsNullOrWhiteSpace(query)) return true;
            return row.Parameters.Values.Any(x => ParameterCodec.Display(x).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void SetAll(bool value)
        {
            foreach (AssignmentRow row in _view.Cast<AssignmentRow>()) row.IsSelected = value;
            if (!value) _grid.SelectedItems.Clear();
            UpdateCounter();
        }

        private List<AssignmentRow> CheckedRows() => _rows.Where(x => x.IsSelected).ToList();

        private AssignmentRow CurrentRow()
        {
            AssignmentRow row = _grid.SelectedItem as AssignmentRow;
            if (row == null) MessageBox.Show(this, "Выберите строку задания.", "Обмен заданиями");
            return row;
        }

        private void AddOrUpdate(object sender, RoutedEventArgs e)
        {
            List<AssignmentRow> rows = CheckedRows();
            if (rows.Count == 0) { MessageBox.Show(this, "Отметьте задания флажками.", "Обмен заданиями"); return; }
            _session.Post(application =>
            {
                OperationReport report = new TaskOperations(application, _settings).AddOrUpdate(rows);
                MovePreviewService.Clear(application);
                MessageBox.Show(this, report.Message, "Добавление / обновление");
                LoadRows();
            });
        }

        private void DeleteLocal(object sender, RoutedEventArgs e)
        {
            List<AssignmentRow> rows = CheckedRows().Where(x => x.LocalElementId > 0).ToList();
            if (rows.Count == 0) { MessageBox.Show(this, "Среди отмеченных строк нет локальных заданий.", "Обмен заданиями"); return; }
            if (MessageBox.Show(this, $"Удалить локальные экземпляры ({rows.Count})?\nСвязанные файлы изменены не будут.", "Обмен заданиями", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _session.Post(application =>
            {
                OperationReport report = new TaskOperations(application, _settings).DeleteLocal(rows);
                MovePreviewService.Clear(application);
                MessageBox.Show(this, report.Message, "Удаление");
                LoadRows();
            });
        }

        private void SelectInModel(object sender, RoutedEventArgs e)
        {
            AssignmentRow row = CurrentRow();
            if (row == null) return;
            TryModelAction(application => new TaskOperations(application, _settings).SelectInModel(row));
        }

        private void ShowInModel(object sender, RoutedEventArgs e)
        {
            AssignmentRow row = CurrentRow();
            if (row == null) return;
            TryModelAction(application => new TaskOperations(application, _settings).ShowInModel(row));
        }

        private void FindCurrentSelection(object sender, RoutedEventArgs e)
        {
            _session.Post(application =>
            {
                var ids = new HashSet<int>(application.ActiveUIDocument.Selection.GetElementIds().Select(RevitId.Value));
                AssignmentRow row = _rows.FirstOrDefault(x => ids.Contains(x.LocalElementId));
                if (row == null) { MessageBox.Show(this, "Выбранный элемент не найден в текущем списке.", "Обмен заданиями"); return; }
                _grid.SelectedItem = row;
                _grid.ScrollIntoView(row);
            });
        }

        private void ShowHistory(object sender, RoutedEventArgs e)
        {
            AssignmentRow row = CurrentRow();
            if (row == null) return;
            _session.Post(application =>
            {
                var history = new StorageService(application.ActiveUIDocument.Document).GetHistory(row.Key);
                var window = new HistoryWindow(history) { Owner = this };
                window.Show();
            });
        }

        private void TryModelAction(Action<UIApplication> action)
        {
            _session.Post(application =>
            {
                try { action(application); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Обмен заданиями"); }
            });
        }

        private void UpdateCounter()
        {
            if (_counter != null && _rows != null)
                _counter.Text = $"Выбрано: {_rows.Count(x => x.IsSelected)} / Всего: {_rows.Count}";
        }
    }
}
