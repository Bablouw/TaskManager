using System;
using System.Globalization;
using Autodesk.Revit.DB;

namespace TaskExchange.Services
{
    internal static class ParameterCodec
    {
        public static string Read(Parameter parameter)
        {
            if (parameter == null || !parameter.HasValue) return "N:";
            switch (parameter.StorageType)
            {
                case StorageType.Double: return "D:" + parameter.AsDouble().ToString("R", CultureInfo.InvariantCulture);
                case StorageType.Integer: return "I:" + parameter.AsInteger().ToString(CultureInfo.InvariantCulture);
                case StorageType.ElementId: return "E:" + RevitId.Value(parameter.AsElementId()).ToString(CultureInfo.InvariantCulture);
                case StorageType.String: return "S:" + (parameter.AsString() ?? string.Empty);
                default: return "N:";
            }
        }

        public static bool Write(Parameter parameter, string encoded)
        {
            if (parameter == null || parameter.IsReadOnly || string.IsNullOrEmpty(encoded) || encoded.Length < 2) return false;
            string value = encoded.Substring(2);
            try
            {
                switch (encoded.Substring(0, 2))
                {
                    case "D:": return parameter.Set(double.Parse(value, CultureInfo.InvariantCulture));
                    case "I:": return parameter.Set(int.Parse(value, CultureInfo.InvariantCulture));
                    case "E:": return parameter.Set(RevitId.Create(int.Parse(value, CultureInfo.InvariantCulture)));
                    case "S:": return parameter.Set(value);
                    default: return false;
                }
            }
            catch { return false; }
        }

        public static string Display(string encoded) =>
            string.IsNullOrEmpty(encoded) || encoded.Length < 2 ? string.Empty : encoded.Substring(2);
    }
}
