using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskExchange.Models
{
    public enum AssignmentStatus
    {
        New,
        Received,
        Moved,
        ParametersChanged,
        Deleted,
        InOtherLink,
        Error
    }

    public sealed class ParameterMapping
    {
        public string TargetParameter { get; set; } = string.Empty;
        public string SourceParameter { get; set; } = string.Empty;
        public bool IsShared { get; set; }
        public string SharedGuid { get; set; } = string.Empty;
    }

    public sealed class SymbolRule
    {
        public string ParameterName { get; set; } = string.Empty;
        public string Condition { get; set; } = "содержит";
        public string Value { get; set; } = string.Empty;
        public string SymbolFamilyName { get; set; } = string.Empty;
        public string SymbolTypeName { get; set; } = string.Empty;
    }

    public sealed class ExchangeConfiguration
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("D");
        public string Name { get; set; } = "Новая конфигурация";
        public List<ParameterMapping> Mappings { get; set; } = new List<ParameterMapping>();
        public List<SymbolRule> SymbolRules { get; set; } = new List<SymbolRule>();

        public override string ToString() => Name;
    }

    public sealed class UserSettings
    {
        public int FormatVersion { get; set; } = 1;
        public string TaskProjectParameterName { get; set; } = "Задание для ЭОМ";
        public List<ExchangeConfiguration> Configurations { get; set; } = new List<ExchangeConfiguration>();
        public Dictionary<string, string> LinkConfigurationIds { get; set; } = new Dictionary<string, string>();
    }

    public sealed class ConfigurationPackage
    {
        public int FormatVersion { get; set; } = 1;
        public List<ExchangeConfiguration> Configurations { get; set; } = new List<ExchangeConfiguration>();
    }

    public sealed class AssignmentSnapshot
    {
        public string Key { get; set; } = string.Empty;
        public string SourceModelId { get; set; } = string.Empty;
        public string SourceModelName { get; set; } = string.Empty;
        public string SourceElementUniqueId { get; set; } = string.Empty;
        public int SourceElementId { get; set; }
        public string LinkInstanceUniqueId { get; set; } = string.Empty;
        public string LinkName { get; set; } = string.Empty;
        public string RecipientModelId { get; set; } = string.Empty;
        public string RecipientModelName { get; set; } = string.Empty;
        public string LocalElementUniqueId { get; set; } = string.Empty;
        public int LocalElementId { get; set; }
        public string ConfigurationId { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Rotation { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public string LoadedAtUtc { get; set; } = string.Empty;
        public string LoadedBy { get; set; } = string.Empty;
        public string LastUpdatedAtUtc { get; set; } = string.Empty;
    }

    public sealed class HistoryEntry
    {
        public string AssignmentKey { get; set; } = string.Empty;
        public string Document { get; set; } = string.Empty;
        public string DateUtc { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Parameters { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ElementUniqueId { get; set; } = string.Empty;
    }

    public sealed class ProjectLedgerData
    {
        public int FormatVersion { get; set; } = 1;
        public List<AssignmentSnapshot> Assignments { get; set; } = new List<AssignmentSnapshot>();
        public List<HistoryEntry> History { get; set; } = new List<HistoryEntry>();
    }

    public sealed class LinkSummary : INotifyPropertyChanged
    {
        private ExchangeConfiguration _configuration;
        public int LinkElementId { get; set; }
        public string LinkUniqueId { get; set; } = string.Empty;
        public string LinkName { get; set; } = string.Empty;
        public bool IsLoaded { get; set; }
        public int Placed { get; set; }
        public int New { get; set; }
        public int Moved { get; set; }
        public int ParametersChanged { get; set; }
        public int Deleted { get; set; }
        public int AtPartners { get; set; }
        public int Total => Placed + New + Moved + ParametersChanged + Deleted;
        public List<ExchangeConfiguration> AvailableConfigurations { get; set; } = new List<ExchangeConfiguration>();

        public ExchangeConfiguration Configuration
        {
            get => _configuration;
            set { _configuration = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class AssignmentRow : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public string Key { get; set; } = string.Empty;
        public int LinkElementId { get; set; }
        public string LinkUniqueId { get; set; } = string.Empty;
        public string LinkName { get; set; } = string.Empty;
        public string SourceModelId { get; set; } = string.Empty;
        public bool IsFromCurrentModel { get; set; }
        public string SourceModelName { get; set; } = string.Empty;
        public int SourceElementId { get; set; }
        public string SourceElementUniqueId { get; set; } = string.Empty;
        public int LocalElementId { get; set; }
        public string LocalElementUniqueId { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string LocalFamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string ConfigurationId { get; set; } = string.Empty;
        public AssignmentStatus Status { get; set; }
        public string StatusText => StatusNames.Get(Status);
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Rotation { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> RuleValues { get; set; } = new Dictionary<string, string>();
        public string LoadedBy { get; set; } = string.Empty;
        public string LoadedAtUtc { get; set; } = string.Empty;
        public string LastUpdatedAtUtc { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public static class StatusNames
    {
        public static string Get(AssignmentStatus status)
        {
            switch (status)
            {
                case AssignmentStatus.New: return "Новое";
                case AssignmentStatus.Received: return "Задание получено";
                case AssignmentStatus.Moved: return "Изменены координаты";
                case AssignmentStatus.ParametersChanged: return "Изменены параметры";
                case AssignmentStatus.Deleted: return "Задание удалено";
                case AssignmentStatus.InOtherLink: return "Есть в другой связи";
                default: return "Ошибка";
            }
        }
    }
}
