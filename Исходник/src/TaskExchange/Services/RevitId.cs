using Autodesk.Revit.DB;

namespace TaskExchange.Services
{
    internal static class RevitId
    {
        public static int Value(ElementId id)
        {
#if REVIT2024
            return checked((int)id.Value);
#else
            return id.IntegerValue;
#endif
        }

        public static ElementId Create(int value)
        {
#if REVIT2024
            return new ElementId((long)value);
#else
            return new ElementId(value);
#endif
        }
    }
}
