using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using TaskExchange.Models;

namespace TaskExchange.Services
{
    public sealed class FamilyCheckResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string FamilyName { get; set; } = "UNI_Задание";
    }

    public sealed class UgoTypeOption
    {
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
    }

    public sealed class AssignmentUgoRequest
    {
        public AssignmentRow Row { get; set; }
        public string UgoFamilyName { get; set; } = string.Empty;
        public string UgoTypeName { get; set; } = string.Empty;
    }

    public sealed class FamilyService
    {
        public const string ExpectedFamilyName = "UNI_Задание";
        public const string DefaultUgoTypeName = "ЭОМ_Эл.вывод";
        private readonly Document _document;

        public FamilyService(Document document) => _document = document;

        public FamilyCheckResult EnsureFamilyLoaded()
        {
            Family existing = FindFamily();
            if (existing != null)
                return new FamilyCheckResult { Success = true, FamilyName = existing.Name };

            string path = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "UNI_Задание.rfa");

            if (!File.Exists(path))
                return new FamilyCheckResult
                {
                    Success = false,
                    Message = "Семейство UNI_Задание не найдено в проекте и отсутствует рядом с плагином:\n" + path
                };

            try
            {
                Family loaded;
                using (var transaction = new Transaction(_document, "Загрузка семейства задания"))
                {
                    transaction.Start();
                    bool result = _document.LoadFamily(path, new FamilyLoadOptions(), out loaded);
                    if (!result || loaded == null)
                    {
                        transaction.RollBack();
                        return new FamilyCheckResult { Success = false, Message = "Revit не смог загрузить семейство UNI_Задание." };
                    }
                    transaction.Commit();
                }
                return new FamilyCheckResult { Success = true, FamilyName = loaded.Name };
            }
            catch (Exception ex)
            {
                LogService.Error("Ошибка загрузки семейства", ex);
                return new FamilyCheckResult { Success = false, Message = "Ошибка загрузки семейства:\n" + ex.Message };
            }
        }

        public Family FindFamily() => new FilteredElementCollector(_document)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .FirstOrDefault(x => string.Equals(x.Name, ExpectedFamilyName, StringComparison.OrdinalIgnoreCase));

        public FamilySymbol FindSymbol(string preferredTypeName = null)
        {
            Family family = FindFamily();
            if (family == null) return null;
            var symbols = family.GetFamilySymbolIds().Select(id => _document.GetElement(id) as FamilySymbol).Where(x => x != null).ToList();
            return symbols.FirstOrDefault(x => string.Equals(x.Name, preferredTypeName, StringComparison.OrdinalIgnoreCase))
                ?? symbols.FirstOrDefault();
        }

        public FamilySymbol FindSymbolExact(string typeName)
        {
            Family family = FindFamily();
            if (family == null) return null;
            return family.GetFamilySymbolIds()
                .Select(id => _document.GetElement(id) as FamilySymbol)
                .FirstOrDefault(x => x != null && string.Equals(x.Name, typeName, StringComparison.OrdinalIgnoreCase));
        }

        public List<UgoTypeOption> GetUgoTypeOptions()
        {
            Family family = FindFamily() ?? throw new InvalidOperationException("Семейство UNI_Задание не найдено.");
            Document familyDocument = null;
            try
            {
                familyDocument = _document.EditFamily(family);
                FamilyParameter parameter = FindUgoParameter(familyDocument);
                return GetNestedUgoSymbols(familyDocument, parameter)
                    .Select(x => new UgoTypeOption { FamilyName = x.Family.Name, TypeName = x.Name })
                    .OrderBy(x => x.TypeName).ToList();
            }
            finally
            {
                if (familyDocument != null && familyDocument.IsValidObject) familyDocument.Close(false);
            }
        }

        public Dictionary<AssignmentRow, string> EnsureUgoTypes(IEnumerable<AssignmentUgoRequest> requests)
        {
            var requested = requests.Where(x => x?.Row != null && !string.IsNullOrWhiteSpace(x.UgoTypeName)).ToList();
            var result = new Dictionary<AssignmentRow, string>();
            if (requested.Count == 0) return result;
            Family family = FindFamily() ?? throw new InvalidOperationException("Семейство UNI_Задание не найдено.");
            Document familyDocument = null;
            try
            {
                familyDocument = _document.EditFamily(family);
                FamilyManager manager = familyDocument.FamilyManager;
                FamilyParameter parameter = FindUgoParameter(familyDocument);
                List<FamilySymbol> available = GetNestedUgoSymbols(familyDocument, parameter);
                var wanted = new Dictionary<AssignmentUgoRequest, FamilySymbol>();
                foreach (AssignmentUgoRequest request in requested)
                {
                    List<FamilySymbol> matches = available.Where(x =>
                        string.Equals(x.Name, request.UgoTypeName, StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrWhiteSpace(request.UgoFamilyName) ||
                         string.Equals(x.Family.Name, request.UgoFamilyName, StringComparison.OrdinalIgnoreCase))).ToList();
                    if (matches.Count != 1)
                        throw new InvalidOperationException("УГО «" + request.UgoTypeName + "» не найдено или неоднозначно в семействе задания.");
                    wanted[request] = matches[0];
                }

                var types = manager.Types.Cast<FamilyType>().ToList();
                bool changed = false;
                using (var transaction = new Transaction(familyDocument, "Типоразмеры задания по УГО"))
                {
                    transaction.Start();
                    foreach (var choice in wanted)
                    {
                        string requestedName = TaskTypeName(choice.Key.Row);
                        FamilyType target = types.FirstOrDefault(x =>
                            string.Equals(x.Name, requestedName, StringComparison.OrdinalIgnoreCase));
                        if (target == null)
                        {
                            target = manager.NewType(requestedName);
                            types.Add(target);
                            changed = true;
                        }
                        manager.CurrentType = target;
                        if (RevitId.Value(UgoTypeId(target, parameter)) != RevitId.Value(choice.Value.Id))
                        {
                            manager.Set(parameter, choice.Value.Id);
                            changed = true;
                        }
                        result[choice.Key.Row] = target.Name;
                    }
                    transaction.Commit();
                }
                if (changed)
                {
                    Family loaded = familyDocument.LoadFamily(_document, new FamilyLoadOptions());
                    if (loaded == null)
                        throw new InvalidOperationException("Не удалось обновить семейство UNI_Задание в текущем проекте.");
                }
                return result;
            }
            finally
            {
                if (familyDocument != null && familyDocument.IsValidObject) familyDocument.Close(false);
            }
        }

        private static FamilyParameter FindUgoParameter(Document familyDocument)
        {
            FamilyParameter parameter = familyDocument.FamilyManager.Parameters.Cast<FamilyParameter>()
                .FirstOrDefault(x => string.Equals(x.Definition?.Name, "УГО", StringComparison.OrdinalIgnoreCase));
            if (parameter == null || parameter.IsInstance || parameter.StorageType != StorageType.ElementId)
                throw new InvalidOperationException("В UNI_Задание нет параметра типа «УГО» с выбором вложенного семейства.");
            return parameter;
        }

        private static List<FamilySymbol> GetNestedUgoSymbols(Document familyDocument, FamilyParameter parameter)
        {
            var all = new FilteredElementCollector(familyDocument).OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x.Family != null && x.Category != null &&
                    RevitId.Value(x.Category.Id) == (int)BuiltInCategory.OST_GenericAnnotation)
                .ToList();
            var usedFamilies = new HashSet<int>();
            foreach (FamilyType type in familyDocument.FamilyManager.Types)
            {
                ElementId id = UgoTypeId(type, parameter);
                FamilySymbol symbol = familyDocument.GetElement(id) as FamilySymbol;
                if (symbol?.Family != null) usedFamilies.Add(RevitId.Value(symbol.Family.Id));
            }
            if (usedFamilies.Count == 0)
                throw new InvalidOperationException("В параметре «УГО» семейства задания не назначено вложенное семейство аннотации.");
            return all.Where(x => usedFamilies.Contains(RevitId.Value(x.Family.Id)))
                .GroupBy(x => RevitId.Value(x.Family.Id) + "|" + x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First()).ToList();
        }

        private static ElementId UgoTypeId(FamilyType type, FamilyParameter parameter)
        {
            try { return type.AsElementId(parameter); }
            catch { return ElementId.InvalidElementId; }
        }

        private static string TaskTypeName(AssignmentRow row)
        {
            string family = string.IsNullOrWhiteSpace(row.FamilyName) ? "Элемент" : row.FamilyName.Trim();
            string type = string.IsNullOrWhiteSpace(row.TypeName) ? "Тип" : row.TypeName.Trim();
            return family + "/" + type;
        }

        private sealed class FamilyLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = false;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Project;
                overwriteParameterValues = false;
                return true;
            }
        }
    }
}
