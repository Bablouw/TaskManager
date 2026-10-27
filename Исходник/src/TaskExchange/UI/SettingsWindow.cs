using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TaskExchange.Models;
using TaskExchange.Services;
using Binding = System.Windows.Data.Binding;
using Grid = System.Windows.Controls.Grid;

namespace TaskExchange.UI
{
    public sealed class SettingsWindow : Window
    {
        internal bool Saved { get; private set; }
        private readonly UserSettings _settings;
        private readonly SettingsStore _store = new SettingsStore();
        private readonly ObservableCollection<ExchangeConfiguration> _configurations;
        private readonly List<string> _parameterNames;
        private readonly List<FamilyParameterItem> _familyParameters;
        private readonly List<UgoTypeOption> _ugoTypes;
        private readonly string _ugoReadError;
        private readonly ObservableCollection<FamilyParameterItem> _availableParameters = new ObservableCollection<FamilyParameterItem>();
        private readonly DataGrid _configurationGrid;
        private readonly DataGrid _parameterGrid;
        private readonly DataGrid _mappingGrid;
        private readonly DataGrid _rulesGrid;
        private readonly System.Windows.Controls.TextBox _taskParameter;

        public SettingsWindow(UIApplication application, UserSettings settings)
        {
            _settings = settings;
            _configurations = new ObservableCollection<ExchangeConfiguration>(
                settings.Configurations.Select(CloneConfiguration));
            _familyParameters = CollectFamilyParameters(application.ActiveUIDocument.Document);
            _parameterNames = CollectProjectParameterNames(application.ActiveUIDocument.Document);
            try { _ugoTypes = new FamilyService(application.ActiveUIDocument.Document).GetUgoTypeOptions(); }
            catch (Exception ex)
            {
                _ugoTypes = new List<UgoTypeOption>();
                _ugoReadError = ex.Message;
                LogService.Error("Не удалось прочитать типоразмеры УГО", ex);
            }
            Title = "UNI — Конфигурации менеджера";
            Width = 1120;
            Height = 700;
            MinWidth = 900;
            MinHeight = 560;
            UiTheme.ApplyWindow(this);

            var root = new Grid { Margin = new Thickness(12) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var heading = new TextBlock { Text = "Конфигурации менеджера", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 2, 4, 12) };
            Grid.SetColumnSpan(heading, 3);
            root.Children.Add(heading);

            var left = new Grid();
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(left, 0);
            Grid.SetRow(left, 1);
            root.Children.Add(left);

            _configurationGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                ItemsSource = _configurations
            };
            UiTheme.ApplyGrid(_configurationGrid);
            _configurationGrid.Columns.Add(new DataGridTextColumn { Header = "Имя конфигурации", Binding = new Binding("Name") { Mode = BindingMode.TwoWay }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _configurationGrid.Columns.Add(new DataGridTextColumn { Header = "GUID", Binding = new Binding("Id"), IsReadOnly = true, Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            _configurationGrid.SelectionChanged += ConfigurationChanged;
            var configPanel = new DockPanel();
            var configButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var addConfiguration = UiHelpers.Button("+ Добавить конфигурацию", AddConfiguration, 155);
            UiTheme.StyleButton(addConfiguration, UiTheme.ButtonTone.Primary);
            configButtons.Children.Add(addConfiguration);
            configButtons.Children.Add(UiHelpers.Button("Переименовать", RenameConfiguration, 120));
            var deleteConfiguration = UiHelpers.Button("Удалить", DeleteConfiguration, 85);
            UiTheme.StyleButton(deleteConfiguration, UiTheme.ButtonTone.Danger);
            configButtons.Children.Add(deleteConfiguration);
            DockPanel.SetDock(configButtons, Dock.Bottom);
            configPanel.Children.Add(configButtons);
            configPanel.Children.Add(_configurationGrid);
            left.Children.Add(Section("Выбор конфигурации", configPanel));

            _parameterGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Extended,
                ItemsSource = _availableParameters
            };
            UiTheme.ApplyGrid(_parameterGrid);
            _parameterGrid.Columns.Add(new DataGridTextColumn { Header = "Имя параметра", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _parameterGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "Общий", Binding = new Binding("IsShared"), Width = 68 });
            _parameterGrid.Columns.Add(new DataGridTextColumn { Header = "GUID", Binding = new Binding("Guid"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            var parameterPanel = new DockPanel();
            var parameterButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            parameterButtons.Children.Add(UiHelpers.Button("+ Общий параметр", AddSharedParameter, 140));
            parameterButtons.Children.Add(UiHelpers.Button("+ Встроенный", AddBuiltInParameter, 120));
            var deleteParameter = UiHelpers.Button("Удалить", DeleteAvailableParameter, 80);
            UiTheme.StyleButton(deleteParameter, UiTheme.ButtonTone.Danger);
            parameterButtons.Children.Add(deleteParameter);
            DockPanel.SetDock(parameterButtons, Dock.Bottom);
            parameterPanel.Children.Add(parameterButtons);
            parameterPanel.Children.Add(_parameterGrid);
            var pool = Section("Добавление параметров", parameterPanel);
            Grid.SetRow(pool, 2);
            left.Children.Add(pool);

            var right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(right, 2);
            Grid.SetRow(right, 1);
            root.Children.Add(right);

            _mappingGrid = CreateMappingGrid();
            UiTheme.ApplyGrid(_mappingGrid);
            var mappingPanel = new DockPanel();
            var mappingButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            mappingButtons.Children.Add(UiHelpers.Button("+ Сопоставление", AddEmptyMapping, 130));
            var deleteMapping = UiHelpers.Button("Удалить сопоставление", DeleteMapping, 160);
            UiTheme.StyleButton(deleteMapping, UiTheme.ButtonTone.Danger);
            mappingButtons.Children.Add(deleteMapping);
            DockPanel.SetDock(mappingButtons, Dock.Bottom);
            mappingPanel.Children.Add(mappingButtons);
            mappingPanel.Children.Add(_mappingGrid);
            right.Children.Add(Section("Сопоставление параметров", mappingPanel));

            _rulesGrid = CreateRulesGrid();
            UiTheme.ApplyGrid(_rulesGrid);
            var rulesPanel = new DockPanel();
            rulesPanel.Children.Add(_rulesGrid);
            var rules = Section("Назначение УГО", rulesPanel, "Правила выбора типоразмера семейства задания");
            Grid.SetRow(rules, 2);
            right.Children.Add(rules);

            var between = new Grid();
            between.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            between.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            between.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var arrows = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            arrows.Children.Add(UiHelpers.Button("→", AddRuleFromSelectedParameter, 32));
            arrows.Children.Add(UiHelpers.Button("←", DeleteRule, 32));
            Grid.SetRow(arrows, 2);
            between.Children.Add(arrows);
            Grid.SetColumn(between, 1);
            Grid.SetRow(between, 1);
            root.Children.Add(between);

            var bottom = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = true };
            var saveButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var saveButton = UiHelpers.Button("Сохранить", Save);
            UiTheme.StyleButton(saveButton, UiTheme.ButtonTone.Primary);
            saveButtons.Children.Add(saveButton);
            saveButtons.Children.Add(UiHelpers.Button("Отмена", (s, e) => Close(), 90));
            DockPanel.SetDock(saveButtons, Dock.Right);
            bottom.Children.Add(saveButtons);
            var taskParameterPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            taskParameterPanel.Children.Add(new TextBlock { Text = "Параметр задания:", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 10, 0) });
            _taskParameter = new System.Windows.Controls.TextBox { Width = 270, Height = 29, Padding = new Thickness(6, 4, 6, 2), Text = settings.TaskProjectParameterName ?? string.Empty, ToolTip = "Введите точное имя параметра проекта. Оно сохраняется локально для пользователя." };
            taskParameterPanel.Children.Add(_taskParameter);
            bottom.Children.Add(taskParameterPanel);
            Grid.SetRow(bottom, 2);
            Grid.SetColumnSpan(bottom, 3);
            root.Children.Add(bottom);

            if (_configurations.Count > 0) _configurationGrid.SelectedIndex = 0;
        }

        private static Border Section(string title, UIElement content, string subtitle = null)
        {
            var panel = new DockPanel { Margin = new Thickness(12, 10, 12, 10) };
            var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            heading.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 13, Foreground = UiTheme.Text });
            if (!string.IsNullOrWhiteSpace(subtitle))
                heading.Children.Add(new TextBlock { Text = subtitle, Foreground = UiTheme.Muted, Margin = new Thickness(0, 2, 0, 0) });
            DockPanel.SetDock(heading, Dock.Top);
            panel.Children.Add(heading);
            panel.Children.Add(content);
            return UiTheme.Card(panel, new Thickness(0));
        }

        private static ExchangeConfiguration CloneConfiguration(ExchangeConfiguration source) =>
            new ExchangeConfiguration
            {
                Id = source.Id,
                Name = source.Name,
                Mappings = source.Mappings.Select(x => new ParameterMapping
                {
                    TargetParameter = x.TargetParameter,
                    SourceParameter = x.SourceParameter,
                    IsShared = x.IsShared,
                    SharedGuid = x.SharedGuid
                }).ToList(),
                SymbolRules = source.SymbolRules.Select(x => new SymbolRule
                {
                    ParameterName = x.ParameterName,
                    Condition = x.Condition,
                    Value = x.Value,
                    SymbolFamilyName = x.SymbolFamilyName,
                    SymbolTypeName = x.SymbolTypeName
                }).ToList()
            };

        private DataGrid CreateMappingGrid()
        {
            var grid = BaseEditableGrid();
            grid.Columns.Add(ComboColumn("Параметр в модели", "TargetParameter", _parameterNames));
            grid.Columns.Add(ComboColumn("Параметр в связи", "SourceParameter", _parameterNames));
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Общий", Binding = new Binding("IsShared"), Width = 70 });
            grid.Columns.Add(new DataGridTextColumn { Header = "GUID общего параметра", Binding = new Binding("SharedGuid"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            return grid;
        }

        private DataGrid CreateRulesGrid()
        {
            var grid = BaseEditableGrid();
            grid.Columns.Add(new DataGridTextColumn { Header = "Имя параметра", Binding = new Binding("ParameterName"), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(ComboColumn("Условие", "Condition", new List<string> { "содержит", "равно", "не равно", "начинается с" }));
            grid.Columns.Add(new DataGridTextColumn { Header = "Значение", Binding = new Binding("Value"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            var symbolCombo = new FrameworkElementFactory(typeof(System.Windows.Controls.ComboBox));
            symbolCombo.SetValue(System.Windows.Controls.ComboBox.ItemsSourceProperty,
                _ugoTypes.Select(x => x.TypeName).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
            symbolCombo.SetValue(System.Windows.Controls.ComboBox.IsEditableProperty, false);
            symbolCombo.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
            symbolCombo.SetBinding(System.Windows.Controls.ComboBox.SelectedItemProperty,
                new Binding("SymbolTypeName") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "УГО (типоразмер)",
                CellTemplate = new DataTemplate { VisualTree = symbolCombo },
                Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
            });
            return grid;
        }

        private static DataGrid BaseEditableGrid() => new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false
        };

        private static DataGridComboBoxColumn ComboColumn(string header, string path, IEnumerable<string> values) => new DataGridComboBoxColumn
        {
            Header = header,
            ItemsSource = values,
            SelectedItemBinding = new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        };

        private void ConfigurationChanged(object sender, SelectionChangedEventArgs e)
        {
            ExchangeConfiguration selected = _configurationGrid.SelectedItem as ExchangeConfiguration;
            _mappingGrid.ItemsSource = selected?.Mappings;
            _rulesGrid.ItemsSource = selected?.SymbolRules;
        }

        private void AddConfiguration(object sender, RoutedEventArgs e)
        {
            string name = PromptConfigurationName("Добавить конфигурацию", "");
            if (name == null) return;
            if (_configurations.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Конфигурация с таким именем уже есть.", "Обмен заданиями");
                return;
            }
            var item = new ExchangeConfiguration { Name = name };
            _configurations.Add(item);
            _configurationGrid.SelectedItem = item;
            _configurationGrid.ScrollIntoView(item);
        }

        private void RenameConfiguration(object sender, RoutedEventArgs e)
        {
            if (!(_configurationGrid.SelectedItem is ExchangeConfiguration selected)) return;
            string name = PromptConfigurationName("Переименовать конфигурацию", selected.Name);
            if (name == null) return;
            if (_configurations.Any(x => x != selected && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Конфигурация с таким именем уже есть.", "Обмен заданиями");
                return;
            }
            selected.Name = name;
            _configurationGrid.Items.Refresh();
        }

        private string PromptConfigurationName(string title, string currentName)
        {
            var dialog = new Window
            {
                Title = title,
                Owner = this,
                Width = 380,
                Height = 190,
                MinWidth = 320,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            UiTheme.ApplyWindow(dialog);
            var panel = new StackPanel { Margin = new Thickness(14) };
            dialog.Content = panel;
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var input = new System.Windows.Controls.TextBox { Text = currentName, Height = 29, Padding = new Thickness(6, 4, 6, 2), Margin = new Thickness(0, 7, 0, 12) };
            buttons.Children.Add(UiHelpers.Button("ОК", (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(input.Text))
                {
                    MessageBox.Show(dialog, "Введите имя конфигурации.", "Обмен заданиями");
                    input.Focus();
                    return;
                }
                dialog.DialogResult = true;
            }, 80));
            buttons.Children.Add(UiHelpers.Button("Отмена", (s, e) => dialog.Close(), 80));
            panel.Children.Add(new TextBlock { Text = "Имя конфигурации", FontWeight = FontWeights.SemiBold });
            panel.Children.Add(input);
            panel.Children.Add(buttons);
            dialog.Loaded += (s, e) => { input.Focus(); input.SelectAll(); };
            return dialog.ShowDialog() == true ? input.Text.Trim() : null;
        }

        private void DeleteConfiguration(object sender, RoutedEventArgs e)
        {
            if (!(_configurationGrid.SelectedItem is ExchangeConfiguration selected)) return;
            if (_configurations.Count == 1)
            {
                MessageBox.Show(this, "Должна остаться хотя бы одна конфигурация.", "Обмен заданиями");
                return;
            }
            _configurations.Remove(selected);
            _configurationGrid.SelectedIndex = 0;
        }

        private void AddSharedParameter(object sender, RoutedEventArgs e) => AddFamilyParameter(true);

        private void AddBuiltInParameter(object sender, RoutedEventArgs e) => AddFamilyParameter(false);

        private void AddFamilyParameter(bool shared)
        {
            var picker = new ParameterPickerWindow(_familyParameters.Where(x => x.IsShared == shared).ToList(), shared) { Owner = this };
            if (picker.ShowDialog() != true) return;
            foreach (FamilyParameterItem parameter in picker.SelectedItems)
            {
                if (_availableParameters.Any(x => x.Name == parameter.Name && x.IsShared == parameter.IsShared)) continue;
                _availableParameters.Add(parameter);
            }
        }

        private void AddMapping(object sender, RoutedEventArgs e)
        {
            if (!(_configurationGrid.SelectedItem is ExchangeConfiguration configuration)) return;
            var selected = _parameterGrid.SelectedItems.OfType<FamilyParameterItem>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Выберите параметры в левом списке.", "Обмен заданиями");
                return;
            }
            foreach (FamilyParameterItem parameter in selected)
            {
                if (configuration.Mappings.Any(x => string.Equals(x.TargetParameter, parameter.Name, StringComparison.OrdinalIgnoreCase))) continue;
                configuration.Mappings.Add(new ParameterMapping { TargetParameter = parameter.Name, SourceParameter = parameter.Name, IsShared = parameter.IsShared, SharedGuid = parameter.Guid });
            }
            _mappingGrid.Items.Refresh();
        }

        private void DeleteAvailableParameter(object sender, RoutedEventArgs e)
        {
            foreach (FamilyParameterItem item in _parameterGrid.SelectedItems.OfType<FamilyParameterItem>().ToList())
                _availableParameters.Remove(item);
        }

        private void AddEmptyMapping(object sender, RoutedEventArgs e)
        {
            if (!(_configurationGrid.SelectedItem is ExchangeConfiguration configuration)) return;
            var mapping = new ParameterMapping();
            configuration.Mappings.Add(mapping);
            _mappingGrid.Items.Refresh();
            _mappingGrid.SelectedItem = mapping;
            _mappingGrid.ScrollIntoView(mapping);
        }

        private void AddRuleFromSelectedParameter(object sender, RoutedEventArgs e)
        {
            if (!(_configurationGrid.SelectedItem is ExchangeConfiguration configuration)) return;
            if (!(_parameterGrid.SelectedItem is FamilyParameterItem parameter))
            {
                MessageBox.Show(this, "Выберите параметр в списке «Добавление параметров».", "Обмен заданиями");
                return;
            }
            if (_ugoTypes.Count == 0)
            {
                MessageBox.Show(this, "Типоразмеры УГО не найдены в семействе задания. " + (_ugoReadError ?? string.Empty), "Обмен заданиями");
                return;
            }
            var rule = new SymbolRule { ParameterName = parameter.Name };
            configuration.SymbolRules.Add(rule);
            _rulesGrid.Items.Refresh();
            _rulesGrid.SelectedItem = rule;
            _rulesGrid.ScrollIntoView(rule);
        }

        private void DeleteRule(object sender, RoutedEventArgs e)
        {
            if (!(_configurationGrid.SelectedItem is ExchangeConfiguration configuration)) return;
            if (!(_rulesGrid.SelectedItem is SymbolRule rule)) return;
            configuration.SymbolRules.Remove(rule);
            _rulesGrid.Items.Refresh();
        }

        private void DeleteMapping(object sender, RoutedEventArgs e)
        {
            if (!(_configurationGrid.SelectedItem is ExchangeConfiguration configuration)) return;
            if (!(_mappingGrid.SelectedItem is ParameterMapping mapping)) return;
            configuration.Mappings.Remove(mapping);
            _mappingGrid.Items.Refresh();
        }

        private void Save(object sender, RoutedEventArgs e)
        {
            _configurationGrid.CommitEdit(DataGridEditingUnit.Row, true);
            _mappingGrid.CommitEdit(DataGridEditingUnit.Row, true);
            _rulesGrid.CommitEdit(DataGridEditingUnit.Row, true);
            string taskParameterName = _taskParameter.Text.Trim();
            if (string.IsNullOrWhiteSpace(taskParameterName))
            {
                MessageBox.Show(this, "Введите имя параметра задания.", "Обмен заданиями");
                _taskParameter.Focus();
                return;
            }
            if (_configurations.Any(x => string.IsNullOrWhiteSpace(x.Name)) ||
                _configurations.Select(x => x.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _configurations.Count)
            {
                MessageBox.Show(this, "У каждой конфигурации должно быть уникальное непустое имя.", "Обмен заданиями");
                return;
            }
            foreach (SymbolRule rule in _configurations.SelectMany(x => x.SymbolRules))
            {
                if (string.IsNullOrWhiteSpace(rule.ParameterName) || string.IsNullOrWhiteSpace(rule.SymbolTypeName))
                {
                    MessageBox.Show(this, "В каждом правиле УГО выберите параметр и типоразмер.", "Обмен заданиями");
                    return;
                }
                List<UgoTypeOption> matches = _ugoTypes.Where(x =>
                    string.Equals(x.TypeName, rule.SymbolTypeName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count != 1)
                {
                    MessageBox.Show(this, "Типоразмер УГО «" + rule.SymbolTypeName + "» отсутствует или неоднозначен в семействе задания.", "Обмен заданиями");
                    return;
                }
                rule.SymbolFamilyName = matches[0].FamilyName;
            }
            _settings.Configurations = _configurations.ToList();
            _settings.TaskProjectParameterName = taskParameterName;
            _store.Save(_settings);
            Saved = true;
            Close();
        }

        private static List<string> CollectProjectParameterNames(Document document)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DefinitionBindingMapIterator iterator = document.ParameterBindings.ForwardIterator();
            while (iterator.MoveNext())
                if (iterator.Key != null) result.Add(iterator.Key.Name);
            foreach (RevitLinkInstance link in new FilteredElementCollector(document).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
            {
                Document linked = link.GetLinkDocument();
                if (linked == null) continue;
                DefinitionBindingMapIterator linkedIterator = linked.ParameterBindings.ForwardIterator();
                while (linkedIterator.MoveNext())
                    if (linkedIterator.Key != null) result.Add(linkedIterator.Key.Name);
            }

            Family family = new FamilyService(document).FindFamily();
            if (family != null)
            {
                foreach (ElementId symbolId in family.GetFamilySymbolIds())
                {
                    Element symbol = document.GetElement(symbolId);
                    if (symbol == null) continue;
                    foreach (Parameter parameter in symbol.Parameters)
                        if (parameter.Definition != null) result.Add(parameter.Definition.Name);
                }

                Document familyDocument = null;
                try
                {
                    familyDocument = document.EditFamily(family);
                    foreach (FamilyParameter parameter in familyDocument.FamilyManager.Parameters)
                        if (parameter.Definition != null) result.Add(parameter.Definition.Name);
                }
                catch (Exception ex)
                {
                    LogService.Error("Не удалось прочитать параметры семейства", ex);
                }
                finally
                {
                    if (familyDocument != null && familyDocument.IsValidObject) familyDocument.Close(false);
                }
            }
            return result.OrderBy(x => x).ToList();
        }

        private static List<FamilyParameterItem> CollectFamilyParameters(Document document)
        {
            var result = new List<FamilyParameterItem>();
            Family family = new FamilyService(document).FindFamily();
            if (family == null) return result;
            Document familyDocument = null;
            try
            {
                familyDocument = document.EditFamily(family);
                foreach (FamilyParameter parameter in familyDocument.FamilyManager.Parameters)
                {
                    string guid = string.Empty;
                    if (parameter.IsShared)
                    {
                        try { guid = parameter.GUID.ToString("D"); }
                        catch { }
                    }
                    result.Add(new FamilyParameterItem
                    {
                        Name = parameter.Definition?.Name ?? string.Empty,
                        IsShared = parameter.IsShared,
                        Guid = guid
                    });
                }
            }
            catch (Exception ex) { LogService.Error("Не удалось получить каталог параметров семейства", ex); }
            finally
            {
                if (familyDocument != null && familyDocument.IsValidObject) familyDocument.Close(false);
            }
            return result.Where(x => !string.IsNullOrWhiteSpace(x.Name)).OrderBy(x => x.Name).ToList();
        }

        internal sealed class FamilyParameterItem
        {
            public string Name { get; set; }
            public bool IsShared { get; set; }
            public string Guid { get; set; }
            public override string ToString() => Name;
        }
    }
}
