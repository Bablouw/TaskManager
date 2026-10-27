using System;
using System.IO;
using TaskExchange.Models;

namespace TaskExchange.Services
{
    public sealed class SettingsStore
    {
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UNI", "TaskExchange");

        public static readonly string SettingsPath = Path.Combine(DirectoryPath, "settings.json");

        public UserSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return CreateDefault();
                UserSettings settings = JsonService.Deserialize<UserSettings>(File.ReadAllText(SettingsPath));
                if (settings.Configurations == null || settings.Configurations.Count == 0)
                    settings.Configurations = CreateDefault().Configurations;
                if (settings.LinkConfigurationIds == null)
                    settings.LinkConfigurationIds = new System.Collections.Generic.Dictionary<string, string>();
                return settings;
            }
            catch (Exception ex)
            {
                LogService.Error("Не удалось прочитать настройки", ex);
                return CreateDefault();
            }
        }

        public void Save(UserSettings settings)
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(SettingsPath, JsonService.Serialize(settings));
        }

        public void Export(string path, UserSettings settings) =>
            File.WriteAllText(path, JsonService.Serialize(new ConfigurationPackage
            {
                Configurations = settings.Configurations
            }));

        public UserSettings Import(string path, UserSettings current)
        {
            ConfigurationPackage package = JsonService.Deserialize<ConfigurationPackage>(File.ReadAllText(path));
            if (package.Configurations == null || package.Configurations.Count == 0)
                throw new InvalidDataException("Файл не содержит конфигураций.");
            current.Configurations = package.Configurations;
            return current;
        }

        private static UserSettings CreateDefault()
        {
            var settings = new UserSettings();
            settings.Configurations.Add(new ExchangeConfiguration { Name = "Основная" });
            return settings;
        }
    }
}
