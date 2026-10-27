using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using TaskExchange.Models;

namespace TaskExchange.Services
{
    public sealed class OperationReport
    {
        public int SuccessCount { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public string Message => Errors.Count == 0
            ? $"Выполнено: {SuccessCount}."
            : $"Выполнено: {SuccessCount}. Ошибок: {Errors.Count}.\n\n" + string.Join("\n", Errors.Take(10));
    }

    public sealed class TaskOperations
    {
        private readonly UIApplication _application;
        private readonly Document _document;
        private readonly UserSettings _settings;
        private readonly StorageService _storage;

        public TaskOperations(UIApplication application, UserSettings settings)
        {
            _application = application;
            _document = application.ActiveUIDocument.Document;
            _settings = settings;
            _storage = new StorageService(_document);
        }

        public OperationReport AddOrUpdate(IEnumerable<AssignmentRow> selected)
        {
            var report = new OperationReport();
            var rows = selected.Where(x => x.Status != AssignmentStatus.Deleted && x.Status != AssignmentStatus.InOtherLink).ToList();
            if (rows.Count == 0) return report;
            ProjectLedgerData ledger = _storage.ReadLedger();
            var familyService = new FamilyService(_document);
            var ugoRequests = rows.Select(row =>
            {
                ExchangeConfiguration configuration = _settings.Configurations.FirstOrDefault(x => x.Id == row.ConfigurationId)
                    ?? _settings.Configurations.FirstOrDefault();
                SymbolRule rule = MatchSymbolRule(row, configuration);
                return new AssignmentUgoRequest
                {
                    Row = row,
                    UgoFamilyName = rule?.SymbolFamilyName ?? string.Empty,
                    UgoTypeName = rule?.SymbolTypeName ?? FamilyService.DefaultUgoTypeName
                };
            }).ToList();
            Dictionary<AssignmentRow, string> ugoTypeNames;
            try
            {
                // Editing and reloading a family must happen before the project transaction.
                ugoTypeNames = familyService.EnsureUgoTypes(ugoRequests);
            }
            catch (Exception ex)
            {
                report.Errors.Add("Не удалось подготовить типоразмеры УГО: " + ex.Message);
                LogService.Error("Ошибка подготовки типоразмеров УГО", ex);
                return report;
            }

            using (var transaction = new Transaction(_document, "Добавление и обновление заданий"))
            {
                transaction.Start();
                foreach (AssignmentRow row in rows)
                {
                    using (var sub = new SubTransaction(_document))
                    {
                        sub.Start();
                        try
                        {
                            FamilySymbol symbol = familyService.FindSymbolExact(ugoTypeNames[row]);
                            if (symbol == null) throw new InvalidOperationException("Не найден тип семейства UNI_Задание.");
                            if (!symbol.IsActive) { symbol.Activate(); _document.Regenerate(); }

                            FamilyInstance local = FindLocal(row) as FamilyInstance;
                            XYZ target = new XYZ(row.X, row.Y, row.Z);
                            string action;
                            if (local == null)
                            {
                                local = _document.Create.NewFamilyInstance(target, symbol, StructuralType.NonStructural);
                                action = "Добавлено";
                            }
                            else
                            {
                                MoveAndRotate(local, target, row.Rotation);
                                if (local.Symbol.Id != symbol.Id) local.Symbol = symbol;
                                action = "Обновлено";
                            }

                            ExchangeConfiguration configuration = _settings.Configurations.FirstOrDefault(x => x.Id == row.ConfigurationId)
                                ?? _settings.Configurations.FirstOrDefault();
                            ApplyParameters(local, row, configuration);

                            AssignmentSnapshot previous = ledger.Assignments.FirstOrDefault(x => x.Key == row.Key);
                            var snapshot = CreateSnapshot(row, local, previous);
                            _storage.WriteAssignment(local, snapshot);
                            _storage.Upsert(ledger, snapshot, action);
                            sub.Commit();
                            report.SuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            sub.RollBack();
                            report.Errors.Add($"{row.LinkName}, ID {row.SourceElementId}: {ex.Message}");
                            LogService.Error("Ошибка обработки задания " + row.Key, ex);
                        }
                    }
                }
                _storage.WriteLedger(ledger);
                transaction.Commit();
            }
            return report;
        }

        public OperationReport DeleteLocal(IEnumerable<AssignmentRow> selected)
        {
            var report = new OperationReport();
            ProjectLedgerData ledger = _storage.ReadLedger();
            using (var transaction = new Transaction(_document, "Удаление локальных заданий"))
            {
                transaction.Start();
                foreach (AssignmentRow row in selected)
                {
                    try
                    {
                        Element local = FindLocal(row);
                        if (local == null) continue;
                        _document.Delete(local.Id);
                        AssignmentSnapshot snapshot = ledger.Assignments.FirstOrDefault(x => x.Key == row.Key) ?? CreateSnapshot(row, local, null);
                        snapshot.LocalElementId = 0;
                        snapshot.LocalElementUniqueId = string.Empty;
                        snapshot.LastUpdatedAtUtc = DateTime.UtcNow.ToString("O");
                        _storage.Upsert(ledger, snapshot, "Удалено из текущей модели");
                        report.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        report.Errors.Add($"{row.LinkName}, ID {row.SourceElementId}: {ex.Message}");
                    }
                }
                _storage.WriteLedger(ledger);
                transaction.Commit();
            }
            return report;
        }

        public void SelectInModel(AssignmentRow row)
        {
            Element element = FindLocal(row);
            if (element == null) throw new InvalidOperationException("Задание ещё не добавлено в текущую модель.");
            _application.ActiveUIDocument.Selection.SetElementIds(new[] { element.Id });
        }

        public void ShowInModel(AssignmentRow row)
        {
            Element element = FindLocal(row);
            if (element == null) throw new InvalidOperationException("Задание ещё не добавлено в текущую модель.");
            UIDocument uiDocument = _application.ActiveUIDocument;
            ViewPlan plan = FindPlanContaining(element, uiDocument.ActiveView);
            if (plan == null)
                throw new InvalidOperationException("Не найден план, на котором видно это задание. Проверьте видимость семейства и диапазон вида.");
            if (RevitId.Value(uiDocument.ActiveView.Id) != RevitId.Value(plan.Id))
                uiDocument.ActiveView = plan;

            if (row.Status != AssignmentStatus.Moved)
            {
                MovePreviewService.Clear(_application);
                ZoomToElement(uiDocument, plan, element);
                uiDocument.Selection.SetElementIds(new[] { element.Id });
                return;
            }

            if (!(element.Location is LocationPoint location))
                throw new InvalidOperationException("Невозможно определить текущее положение локального задания.");
            XYZ current = location.Point;
            // A floor plan only displays a limited height range. Show the target XY at
            // the local assignment's visible height; Add/Update still uses the full XYZ.
            XYZ destinationOnPlan = new XYZ(row.X, row.Y, current.Z);
            ZoomToMovement(uiDocument, plan, current, destinationOnPlan);
            MovePreviewService.Show(_application, uiDocument, plan, current, destinationOnPlan);
            uiDocument.Selection.SetElementIds(new[] { element.Id });
        }

        private ViewPlan FindPlanContaining(Element element, View activeView)
        {
            int elementId = RevitId.Value(element.Id);
            var plans = new FilteredElementCollector(_document)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(x => !x.IsTemplate && IsPlanType(x.ViewType))
                .OrderBy(x => activeView != null && RevitId.Value(x.Id) == RevitId.Value(activeView.Id) ? 0 : 1)
                .ThenBy(x => x.ViewType == ViewType.FloorPlan ? 0 : x.ViewType == ViewType.EngineeringPlan ? 1 : 2)
                .ThenBy(x => x.Name)
                .ToList();

            foreach (ViewPlan plan in plans)
            {
                if (!FilteredElementCollector.IsViewValidForElementIteration(_document, plan.Id)) continue;
                try
                {
                    if (new FilteredElementCollector(_document, plan.Id)
                        .WhereElementIsNotElementType()
                        .ToElementIds()
                        .Any(id => RevitId.Value(id) == elementId))
                        return plan;
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    // Some generated or dependent views cannot be queried in this way.
                }
            }
            return null;
        }

        private static bool IsPlanType(ViewType viewType) =>
            viewType == ViewType.FloorPlan || viewType == ViewType.EngineeringPlan || viewType == ViewType.CeilingPlan;

        private static void ZoomToElement(UIDocument uiDocument, ViewPlan plan, Element element)
        {
            UIView uiView = FindUiView(uiDocument, plan);
            if (uiView == null) return;
            BoundingBoxXYZ bounds = element.get_BoundingBox(plan);
            XYZ center = bounds == null
                ? (element.Location as LocationPoint)?.Point ?? plan.Origin
                : (bounds.Min + bounds.Max).Multiply(0.5);
            XYZ right = plan.RightDirection.Normalize();
            XYZ up = plan.UpDirection.Normalize();
            const double halfSize = 5.0;
            uiView.ZoomAndCenterRectangle(center - right.Multiply(halfSize) - up.Multiply(halfSize),
                center + right.Multiply(halfSize) + up.Multiply(halfSize));
        }

        private static void ZoomToMovement(UIDocument uiDocument, View view, XYZ current, XYZ destination)
        {
            UIView uiView = FindUiView(uiDocument, view);
            if (uiView == null) return;
            XYZ right = view.RightDirection.Normalize();
            XYZ up = view.UpDirection.Normalize();
            XYZ delta = destination - current;
            double distance = current.DistanceTo(destination);
            double margin = Math.Max(3.0, distance * 0.2);
            double halfWidth = Math.Abs(delta.DotProduct(right)) * 0.5 + margin;
            double halfHeight = Math.Abs(delta.DotProduct(up)) * 0.5 + margin;
            XYZ center = (current + destination).Multiply(0.5);
            uiView.ZoomAndCenterRectangle(center - right.Multiply(halfWidth) - up.Multiply(halfHeight),
                center + right.Multiply(halfWidth) + up.Multiply(halfHeight));
        }

        private static UIView FindUiView(UIDocument uiDocument, View view) =>
            uiDocument.GetOpenUIViews().FirstOrDefault(x => RevitId.Value(x.ViewId) == RevitId.Value(view.Id));

        private Element FindLocal(AssignmentRow row)
        {
            if (!string.IsNullOrWhiteSpace(row.LocalElementUniqueId))
            {
                try
                {
                    Element element = _document.GetElement(row.LocalElementUniqueId);
                    if (element != null) return element;
                }
                catch { }
            }
            return row.LocalElementId > 0 ? _document.GetElement(RevitId.Create(row.LocalElementId)) : null;
        }

        private static void MoveAndRotate(FamilyInstance instance, XYZ target, double targetRotation)
        {
            if (!(instance.Location is LocationPoint location))
                throw new InvalidOperationException("Поддерживаются только точечные экземпляры семейства задания.");
            XYZ delta = target - location.Point;
            if (!delta.IsZeroLength()) ElementTransformUtils.MoveElement(instance.Document, instance.Id, delta);
            double rotationDelta = NormalizeAngle(targetRotation - location.Rotation);
            if (Math.Abs(rotationDelta) > 1e-8)
            {
                Line axis = Line.CreateBound(target, target + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(instance.Document, instance.Id, axis, rotationDelta);
            }
        }

        private static double NormalizeAngle(double value)
        {
            while (value > Math.PI) value -= 2 * Math.PI;
            while (value < -Math.PI) value += 2 * Math.PI;
            return value;
        }

        private static void ApplyParameters(FamilyInstance local, AssignmentRow row, ExchangeConfiguration configuration)
        {
            if (configuration == null) return;
            foreach (ParameterMapping mapping in configuration.Mappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.TargetParameter) || string.IsNullOrWhiteSpace(mapping.SourceParameter)) continue;
                if (!row.Parameters.TryGetValue(mapping.SourceParameter, out string value)) continue;
                ParameterCodec.Write(local.LookupParameter(mapping.TargetParameter), value);
            }
        }

        private static SymbolRule MatchSymbolRule(AssignmentRow row, ExchangeConfiguration configuration)
        {
            if (configuration == null) return null;
            foreach (SymbolRule rule in configuration.SymbolRules)
            {
                if (string.IsNullOrWhiteSpace(rule.ParameterName) || string.IsNullOrWhiteSpace(rule.SymbolTypeName)) continue;
                if (string.IsNullOrWhiteSpace(rule.Value)) continue;
                if (!row.Parameters.TryGetValue(rule.ParameterName, out string encoded)) continue;
                string actual = row.RuleValues.TryGetValue(rule.ParameterName, out string display)
                    ? display : ParameterCodec.Display(encoded);
                bool matches;
                switch ((rule.Condition ?? string.Empty).ToLowerInvariant())
                {
                    case "равно": matches = string.Equals(actual, rule.Value, StringComparison.OrdinalIgnoreCase); break;
                    case "не равно": matches = !string.Equals(actual, rule.Value, StringComparison.OrdinalIgnoreCase); break;
                    case "начинается с": matches = actual.StartsWith(rule.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase); break;
                    default: matches = actual.IndexOf(rule.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0; break;
                }
                if (!matches) continue;
                return rule;
            }
            return null;
        }

        private AssignmentSnapshot CreateSnapshot(AssignmentRow row, Element local, AssignmentSnapshot previous)
        {
            DateTime now = DateTime.UtcNow;
            return new AssignmentSnapshot
            {
                Key = row.Key,
                SourceModelId = row.SourceModelId,
                SourceModelName = row.SourceModelName,
                SourceElementUniqueId = row.SourceElementUniqueId,
                SourceElementId = row.SourceElementId,
                LinkInstanceUniqueId = row.LinkUniqueId,
                LinkName = row.LinkName,
                RecipientModelId = _storage.CurrentModelId,
                RecipientModelName = _document.Title,
                LocalElementUniqueId = local?.UniqueId ?? string.Empty,
                LocalElementId = local == null ? 0 : RevitId.Value(local.Id),
                ConfigurationId = row.ConfigurationId,
                FamilyName = FamilyService.ExpectedFamilyName,
                TypeName = row.TypeName,
                X = row.X,
                Y = row.Y,
                Z = row.Z,
                Rotation = row.Rotation,
                Parameters = new Dictionary<string, string>(row.Parameters),
                LoadedAtUtc = string.IsNullOrWhiteSpace(previous?.LoadedAtUtc) ? now.ToString("O") : previous.LoadedAtUtc,
                LoadedBy = string.IsNullOrWhiteSpace(previous?.LoadedBy) ? _document.Application.Username : previous.LoadedBy,
                LastUpdatedAtUtc = now.ToString("O")
            };
        }
    }
}
