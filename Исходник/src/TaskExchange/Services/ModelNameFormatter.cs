using System;

namespace TaskExchange.Services
{
    internal static class ModelNameFormatter
    {
        public static string ForDisplay(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            int extensionIndex = value.IndexOf(".rvt", StringComparison.OrdinalIgnoreCase);
            return extensionIndex < 0
                ? value.Trim()
                : value.Substring(0, extensionIndex + 4).Trim();
        }

        public static string ConfigurationKey(string value) =>
            ForDisplay(value).ToUpperInvariant();
    }
}
