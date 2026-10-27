using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using TaskExchange.Models;

namespace TaskExchange.Services
{
    public sealed class AssignmentScanner
    {
        private const double PositionToleranceFeet = 1.0 / 304.8;
        private readonly Document _document;
        private readonly StorageService _storage;
        private readonly UserSettings _settings;

        public AssignmentScanner(Document document, UserSettings settings)
        {
            _document = document;
            _storage = new StorageService(document);
            _settings = settings;
        }

        public List<LinkSummary> ScanLinks()
        {
            var links = new FilteredElementCollector(_document)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .OrderBy(x => x.Name)
                .ToList();

            var summaries = new List<LinkSummary>();
            foreach (RevitLinkInstance link in links)
            {
                ExchangeConfiguration configuration = ResolveConfiguration(link.UniqueId);
                List<AssignmentRow> rows = ScanAssignments(new[] { RevitId.Value(link.Id) }, configuration);
                summaries.Add(new LinkSummary
                {
                    LinkElementId = RevitId.Value(link.Id),
                    LinkUniqueId = link.UniqueId,
                    LinkName = link.Name,
                    IsLoaded = link.GetLinkDocument() != null,
                    Placed = rows.Count(x => x.Status == AssignmentStatus.Received),
                    New = rows.Count(x => x.Status == AssignmentStatus.New),
                    Moved = rows.Count(x => x.Status == AssignmentStatus.Moved),
                    ParametersChanged = rows.Count(x => x.Status == AssignmentStatus.ParametersChanged),
                    Deleted = rows.Count(x => x.Status == AssignmentStatus.Deleted),
                    AtPartners = CountAtPartners(link),
                    AvailableConfigurations = _settings.Configurations,
                    Configuration = configuration
                });
            }
            return summaries;
        }

        public List<AssignmentRow> ScanAssignments(IEnumerable<int> linkIds, ExchangeConfiguration fallbackConfiguration = null)
        {
            var idSet = new HashSet<int>(linkIds);
            var result = new List<AssignmentRow>();
            ProjectLedgerData ledger = _storage.ReadLedger();
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (RevitLinkInstance link in new FilteredElementCollector(_document)
                .OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()
                .Where(x => idSet.Contains(RevitId.Value(x.Id))))
            {
                Document linkedDocument = link.GetLinkDocument();
                if (linkedDocument == null) continue;
                ExchangeConfiguration configuration = ResolveConfiguration(link.UniqueId) ?? fallbackConfiguration;
                Transform transform = link.GetTotalTransform();
                string sourceModelId = linkedDocument.ProjectInformation?.UniqueId ?? linkedDocument.Title;

                var sourceItems = new FilteredElementCollector(linkedDocument)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Where(IsTaskForCurrentDiscipline)
                    .ToList();

                foreach (Element source in sourceItems)
                {
                    if (!TryGetPlacement(source, out XYZ sourcePoint, out double sourceRotation)) continue;
                    XYZ point = transform.OfPoint(sourcePoint);
                    GetSourceNames(linkedDocument, source, out string familyName, out string typeName);
                    string key = MakeKey(link.UniqueId, sourceModelId, source.UniqueId);
                    seenKeys.Add(key);
                    AssignmentSnapshot ledgerItem = ledger.Assignments.FirstOrDefault(x => x.Key == key);
                    Element localElement = FindLocalElement(ledgerItem);
                    AssignmentSnapshot metadata = ledgerItem ?? _storage.ReadAssignment(localElement);
                    AssignmentSnapshot linkedTask = new StorageService(linkedDocument).ReadAssignment(source);
                    Dictionary<string, string> parameters = ReadParameters(linkedDocument, source, configuration);
                    AssignmentStatus status = DetermineStatus(localElement, ledgerItem, point, parameters);

                    result.Add(new AssignmentRow
                    {
                        Key = key,
                        LinkElementId = RevitId.Value(link.Id),
                        LinkUniqueId = link.UniqueId,
                        LinkName = link.Name,
                        SourceModelId = sourceModelId,
                        IsFromCurrentModel = linkedTask != null && string.Equals(linkedTask.SourceModelId, _storage.CurrentModelId, StringComparison.Ordinal),
                        SourceModelName = linkedDocument.Title,
                        SourceElementId = RevitId.Value(source.Id),
                        SourceElementUniqueId = source.UniqueId,
                        LocalElementId = localElement == null ? 0 : RevitId.Value(localElement.Id),
                        LocalElementUniqueId = localElement?.UniqueId ?? string.Empty,
                        FamilyName = familyName,
                        LocalFamilyName = (localElement as FamilyInstance)?.Symbol?.Family?.Name ?? string.Empty,
                        TypeName = typeName,
                        LoadedBy = metadata?.LoadedBy ?? string.Empty,
                        LoadedAtUtc = metadata?.LoadedAtUtc ?? string.Empty,
                        LastUpdatedAtUtc = metadata?.LastUpdatedAtUtc ?? string.Empty,
                        ConfigurationId = configuration?.Id ?? string.Empty,
                        Status = status,
                        X = point.X,
                        Y = point.Y,
                        Z = point.Z,
                        Rotation = sourceRotation + Math.Atan2(transform.BasisX.Y, transform.BasisX.X),
                        Parameters = parameters,
                        RuleValues = ReadRuleValues(linkedDocument, source, configuration)
                    });
                }
            }

            foreach (AssignmentSnapshot old in ledger.Assignments.Where(x => idSet.Contains(FindLinkId(x.LinkInstanceUniqueId))))
            {
                if (seenKeys.Contains(old.Key)) continue;
                Element local = FindLocalElement(old);
                result.Add(new AssignmentRow
                {
                    Key = old.Key,
                    LinkElementId = FindLinkId(old.LinkInstanceUniqueId),
                    LinkUniqueId = old.LinkInstanceUniqueId,
                    LinkName = old.LinkName,
                    SourceModelId = old.SourceModelId,
                    SourceModelName = old.SourceModelName,
                    SourceElementId = old.SourceElementId,
                    SourceElementUniqueId = old.SourceElementUniqueId,
                    LocalElementId = local == null ? old.LocalElementId : RevitId.Value(local.Id),
                    LocalElementUniqueId = local?.UniqueId ?? old.LocalElementUniqueId,
                    FamilyName = old.FamilyName,
                    LocalFamilyName = (local as FamilyInstance)?.Symbol?.Family?.Name ?? string.Empty,
                    TypeName = old.TypeName,
                    LoadedBy = old.LoadedBy,
                    LoadedAtUtc = old.LoadedAtUtc,
                    LastUpdatedAtUtc = old.LastUpdatedAtUtc,
                    ConfigurationId = old.ConfigurationId,
                    Status = AssignmentStatus.Deleted,
                    X = old.X,
                    Y = old.Y,
                    Z = old.Z,
                    Rotation = old.Rotation,
                    Parameters = old.Parameters ?? new Dictionary<string, string>()
                });
            }

            MarkDuplicateSources(result);
            return result;
        }

        private bool IsTaskForCurrentDiscipline(Element instance)
        {
            string name = _settings.TaskProjectParameterName;
            if (string.IsNullOrWhiteSpace(name)) return true;
            Parameter parameter = FindInstanceOrTypeParameter(instance.Document, instance, name);
            if (parameter == null) return false;
            return parameter.StorageType == StorageType.Integer
                ? parameter.AsInteger() != 0
                : string.Equals(parameter.AsValueString(), "Да", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(parameter.AsString(), "1", StringComparison.OrdinalIgnoreCase);
        }

        private ExchangeConfiguration ResolveConfiguration(string linkUniqueId)
        {
            string id;
            if (_settings.LinkConfigurationIds.TryGetValue(linkUniqueId, out id))
            {
                ExchangeConfiguration found = _settings.Configurations.FirstOrDefault(x => x.Id == id);
                if (found != null) return found;
            }
            return _settings.Configurations.FirstOrDefault();
        }

        private Dictionary<string, string> ReadParameters(Document sourceDocument, Element source, ExchangeConfiguration configuration)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (configuration == null) return values;
            foreach (ParameterMapping mapping in configuration.Mappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.SourceParameter)) continue;
                Parameter parameter = FindInstanceOrTypeParameter(sourceDocument, source, mapping.SourceParameter);
                values[mapping.SourceParameter] = ParameterCodec.Read(parameter);
            }
            foreach (string name in configuration.SymbolRules.Select(x => x.ParameterName)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!values.ContainsKey(name)) values[name] = ParameterCodec.Read(FindInstanceOrTypeParameter(sourceDocument, source, name));
            }
            return values;
        }

        private static Parameter FindInstanceOrTypeParameter(Document document, Element source, string name)
        {
            Parameter instanceParameter = source.LookupParameter(name);
            if (instanceParameter != null) return instanceParameter;
            ElementId typeId = source.GetTypeId();
            return typeId == ElementId.InvalidElementId ? null : document.GetElement(typeId)?.LookupParameter(name);
        }

        private static Dictionary<string, string> ReadRuleValues(Document document, Element source, ExchangeConfiguration configuration)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (configuration == null) return values;
            foreach (string name in configuration.SymbolRules.Select(x => x.ParameterName)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Parameter parameter = FindInstanceOrTypeParameter(document, source, name);
                string display = parameter?.AsValueString();
                values[name] = string.IsNullOrWhiteSpace(display)
                    ? ParameterCodec.Display(ParameterCodec.Read(parameter))
                    : display;
            }
            return values;
        }

        private AssignmentStatus DetermineStatus(Element local, AssignmentSnapshot old, XYZ point, Dictionary<string, string> parameters)
        {
            if (local == null) return AssignmentStatus.New;
            if (old == null) old = _storage.ReadAssignment(local);
            if (old == null) return AssignmentStatus.ParametersChanged;
            double dx = old.X - point.X, dy = old.Y - point.Y, dz = old.Z - point.Z;
            if (Math.Sqrt(dx * dx + dy * dy + dz * dz) > PositionToleranceFeet) return AssignmentStatus.Moved;
            if (!DictionariesEqual(old.Parameters, parameters)) return AssignmentStatus.ParametersChanged;
            return AssignmentStatus.Received;
        }

        private Element FindLocalElement(AssignmentSnapshot snapshot)
        {
            if (snapshot == null) return null;
            if (!string.IsNullOrWhiteSpace(snapshot.LocalElementUniqueId))
            {
                try { return _document.GetElement(snapshot.LocalElementUniqueId); }
                catch { }
            }
            return snapshot.LocalElementId > 0 ? _document.GetElement(RevitId.Create(snapshot.LocalElementId)) : null;
        }

        private int FindLinkId(string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(uniqueId)) return 0;
            try { Element item = _document.GetElement(uniqueId); return item == null ? 0 : RevitId.Value(item.Id); }
            catch { return 0; }
        }

        private int CountAtPartners(RevitLinkInstance link)
        {
            Document linked = link.GetLinkDocument();
            if (linked == null) return 0;
            string currentId = _storage.CurrentModelId;
            return new FilteredElementCollector(linked).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .Count(x =>
                {
                    AssignmentSnapshot data = new StorageService(linked).ReadAssignment(x);
                    return data != null && string.Equals(data.SourceModelId, currentId, StringComparison.Ordinal);
                });
        }

        private static bool DictionariesEqual(Dictionary<string, string> left, Dictionary<string, string> right)
        {
            left = left ?? new Dictionary<string, string>();
            right = right ?? new Dictionary<string, string>();
            return left.Count == right.Count && left.All(x => right.TryGetValue(x.Key, out string value) && value == x.Value);
        }

        private static string MakeKey(string linkId, string modelId, string elementId) => linkId + "|" + modelId + "|" + elementId;

        private static bool TryGetPlacement(Element element, out XYZ point, out double rotation)
        {
            point = null;
            rotation = 0;
            if (element.Location is LocationPoint locationPoint)
            {
                point = locationPoint.Point;
                rotation = locationPoint.Rotation;
                return point != null;
            }

            if (element.Location is LocationCurve locationCurve && locationCurve.Curve != null)
            {
                Curve curve = locationCurve.Curve;
                point = curve.Evaluate(0.5, true);
                try
                {
                    Transform derivatives = curve.ComputeDerivatives(0.5, true);
                    XYZ tangent = derivatives.BasisX;
                    rotation = Math.Atan2(tangent.Y, tangent.X);
                }
                catch { rotation = 0; }
                return point != null;
            }

            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return false;
            point = (box.Min + box.Max) * 0.5;
            return true;
        }

        private static void GetSourceNames(Document sourceDocument, Element element, out string familyName, out string typeName)
        {
            familyName = element.Category?.Name ?? element.GetType().Name;
            typeName = element.Name ?? string.Empty;
            if (element is FamilyInstance familyInstance && familyInstance.Symbol?.Family != null)
            {
                familyName = familyInstance.Symbol.Family.Name;
                typeName = familyInstance.Symbol.Name;
                return;
            }

            ElementType elementType = sourceDocument.GetElement(element.GetTypeId()) as ElementType;
            if (elementType == null) return;
            if (!string.IsNullOrWhiteSpace(elementType.FamilyName)) familyName = elementType.FamilyName;
            typeName = elementType.Name;
        }

        private static void MarkDuplicateSources(List<AssignmentRow> rows)
        {
            foreach (var group in rows.Where(x => x.Status != AssignmentStatus.Deleted)
                .GroupBy(x => x.SourceModelId + "|" + x.SourceElementUniqueId).Where(x => x.Count() > 1))
            {
                foreach (AssignmentRow row in group.Where(x => x.Status == AssignmentStatus.New).Skip(1))
                    row.Status = AssignmentStatus.InOtherLink;
            }
        }
    }
}
