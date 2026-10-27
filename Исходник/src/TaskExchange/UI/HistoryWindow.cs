using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TaskExchange.Models;

namespace TaskExchange.UI
{
    public sealed class HistoryWindow : Window
    {
        public HistoryWindow(IReadOnlyList<HistoryEntry> history)
        {
            Title = "UNI — История версий выбранного задания";
            Width = 900;
            Height = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var grid = new DataGrid
            {
                Margin = new Thickness(10),
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                ItemsSource = history
            };
            grid.Columns.Add(Column("Документ", "Document", 1.2));
            grid.Columns.Add(Column("Дата UTC", "DateUtc", 1.1));
            grid.Columns.Add(Column("Пользователь", "User", 1));
            grid.Columns.Add(Column("Действие", "Action", 1));
            grid.Columns.Add(Column("Параметры", "Parameters", 2));
            grid.Columns.Add(Column("Координаты", "Location", 1.4));
            grid.Columns.Add(Column("GUID", "ElementUniqueId", 1.5));
            Content = grid;
        }

        private static DataGridTextColumn Column(string header, string path, double width) => new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(path),
            Width = new DataGridLength(width, DataGridLengthUnitType.Star)
        };
    }
}
