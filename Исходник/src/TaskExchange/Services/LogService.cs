using System;
using System.IO;

namespace TaskExchange.Services
{
    internal static class LogService
    {
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UNI", "TaskExchange", "Logs");

        public static void Info(string message) => Write("INFO", message, null);
        public static void Error(string message, Exception exception) => Write("ERROR", message, exception);

        private static void Write(string level, string message, Exception exception)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                string path = Path.Combine(DirectoryPath, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                File.AppendAllText(path,
                    $"{DateTime.Now:O} [{level}] {message}{Environment.NewLine}{exception}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
