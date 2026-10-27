using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.UI;

namespace TaskExchange.Services
{
    // Uses the same Revit-native analysis graphics approach as the reference task
    // manager. The graphics stay attached to model coordinates while zooming and
    // panning, and no family instances or annotation labels are created.
    internal static class MovePreviewService
    {
        private const string VectorStyleName = "UNI Обмен заданиями — перемещение";
        private const string SurfaceStyleName = "UNI Обмен заданиями — положение";
        private static Document _document;
        private static ElementId _viewId;

        public static void Show(UIApplication application, UIDocument uiDocument, View view,
            XYZ current, XYZ destination)
        {
            Document document = uiDocument.Document;
            if (!view.AllowsAnalysisDisplay())
                throw new InvalidOperationException("Выбранный план не поддерживает временную аналитическую графику.");
            if (current.DistanceTo(destination) < 1e-6)
                throw new InvalidOperationException("Текущее и новое положения задания совпадают.");

            using (var transaction = new Transaction(document, "Показать перемещение задания"))
            {
                transaction.Start();
                ClearCurrentManager();

                SpatialFieldManager manager = SpatialFieldManager.GetSpatialFieldManager(view)
                    ?? SpatialFieldManager.CreateSpatialFieldManager(view, 1);
                manager.Clear();

                var green = new Autodesk.Revit.DB.Color(0, 255, 0);
                AnalysisDisplayStyle vectorStyle = GetOrCreateVectorStyle(document, green);
                AnalysisDisplayStyle surfaceStyle = GetOrCreateSurfaceStyle(document, green);
                PaintVector(manager, Line.CreateBound(current, destination), vectorStyle,
                    "UNI_Перемещение_" + Guid.NewGuid().ToString("N"));
                PaintSolid(manager, CreateSphere(current, 1.5), surfaceStyle,
                    "UNI_Текущее_" + Guid.NewGuid().ToString("N"));
                PaintSolid(manager, CreateSphere(destination, 1.5), surfaceStyle,
                    "UNI_Новое_" + Guid.NewGuid().ToString("N"));

                transaction.Commit();
                _document = document;
                _viewId = view.Id;
            }

            uiDocument.RefreshActiveView();
            LogService.Info($"Предпросмотр AVF: план '{view.Name}', от {current} к {destination}.");
        }

        public static void Clear(UIApplication application)
        {
            Document document = _document;
            ElementId viewId = _viewId;
            Drop();
            if (document == null || viewId == null || !document.IsValidObject) return;
            View view = document.GetElement(viewId) as View;
            if (view == null || !view.IsValidObject) return;
            SpatialFieldManager manager = SpatialFieldManager.GetSpatialFieldManager(view);
            if (manager == null) return;

            using (var transaction = new Transaction(document, "Скрыть перемещение задания"))
            {
                transaction.Start();
                manager.Clear();
                transaction.Commit();
            }
            try { application?.ActiveUIDocument?.RefreshActiveView(); }
            catch { /* The view may be closing. */ }
        }

        // Only forget managed references here. Revit API cleanup is performed by
        // Clear from an ExternalEvent callback while the document is still valid.
        public static void Drop()
        {
            _document = null;
            _viewId = null;
        }

        private static void ClearCurrentManager()
        {
            if (_document == null || _viewId == null || !_document.IsValidObject) return;
            View previousView = _document.GetElement(_viewId) as View;
            if (previousView?.IsValidObject != true) return;
            SpatialFieldManager.GetSpatialFieldManager(previousView)?.Clear();
            Drop();
        }

        private static AnalysisDisplayStyle GetOrCreateVectorStyle(Document document,
            Autodesk.Revit.DB.Color color)
        {
            AnalysisDisplayStyle existing = FindStyle(document, VectorStyleName);
            if (existing != null) return existing;
            var vectors = new AnalysisDisplayVectorSettings
            {
                ArrowLineWeight = 1,
                VectorPosition = (AnalysisDisplayStyleVectorPosition)1,
                ArrowheadScale = (AnalysisDisplayStyleVectorArrowheadScale)0
            };
            var colors = new AnalysisDisplayColorSettings { MinColor = color, MaxColor = color };
            var legend = new AnalysisDisplayLegendSettings { ShowLegend = false, Rounding = 0.05 };
            return AnalysisDisplayStyle.CreateAnalysisDisplayStyle(document, VectorStyleName,
                vectors, colors, legend);
        }

        private static AnalysisDisplayStyle GetOrCreateSurfaceStyle(Document document,
            Autodesk.Revit.DB.Color color)
        {
            AnalysisDisplayStyle existing = FindStyle(document, SurfaceStyleName);
            if (existing != null) return existing;
            var surfaces = new AnalysisDisplayColoredSurfaceSettings
            {
                ShowGridLines = false,
                ShowContourLines = false,
                Transparency = 80
            };
            var colors = new AnalysisDisplayColorSettings { MinColor = color, MaxColor = color };
            var legend = new AnalysisDisplayLegendSettings { ShowLegend = false, Rounding = 0.05 };
            return AnalysisDisplayStyle.CreateAnalysisDisplayStyle(document, SurfaceStyleName,
                surfaces, colors, legend);
        }

        private static AnalysisDisplayStyle FindStyle(Document document, string name) =>
            new FilteredElementCollector(document)
                .OfClass(typeof(AnalysisDisplayStyle))
                .Cast<AnalysisDisplayStyle>()
                .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

        private static void PaintVector(SpatialFieldManager manager, Curve curve,
            AnalysisDisplayStyle style, string schemaName)
        {
            var schema = new AnalysisResultSchema(schemaName, "Перемещение задания")
            {
                AnalysisDisplayStyleId = style.Id
            };
            int resultIndex = manager.RegisterResult(schema);
            int primitive = manager.AddSpatialFieldPrimitive(curve, Transform.Identity);
            var parameters = new List<double>
            {
                curve.GetEndParameter(0),
                curve.GetEndParameter(1)
            };
            XYZ direction = curve.ComputeDerivatives(0, true).BasisX.Normalize();
            double length = curve.Length;
            var values = new List<VectorAtPoint>
            {
                new VectorAtPoint(new List<XYZ> { direction.Multiply(length / 9.0) }),
                new VectorAtPoint(new List<XYZ> { direction.Multiply(length / 18.0) })
            };
            manager.UpdateSpatialFieldPrimitive(primitive,
                new FieldDomainPointsByParameter(parameters), new FieldValues(values), resultIndex);
        }

        private static void PaintSolid(SpatialFieldManager manager, Solid solid,
            AnalysisDisplayStyle style, string schemaName)
        {
            var schema = new AnalysisResultSchema(schemaName, "Положение задания");
            schema.SetUnits(new List<string> { "Feet" }, new List<double> { 1.0 });
            schema.AnalysisDisplayStyleId = style.Id;
            int resultIndex = manager.RegisterResult(schema);

            foreach (Face face in solid.Faces)
            {
                int primitive = manager.AddSpatialFieldPrimitive(face, Transform.Identity);
                BoundingBoxUV bounds = face.GetBoundingBox();
                var points = new List<UV>
                {
                    new UV(bounds.Min.U, bounds.Min.V),
                    new UV(bounds.Min.U, bounds.Max.V),
                    new UV(bounds.Max.U, bounds.Min.V),
                    new UV(bounds.Max.U, bounds.Max.V)
                };
                var values = points.Select(x =>
                    new ValueAtPoint(new List<double> { 1.0 })).ToList();
                manager.UpdateSpatialFieldPrimitive(primitive,
                    new FieldDomainPointsByUV(points), new FieldValues(values), resultIndex);
            }
        }

        private static Solid CreateSphere(XYZ center, double radius)
        {
            var frame = new Frame(center, XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ);
            Arc arc = Arc.Create(center - radius * XYZ.BasisZ,
                center + radius * XYZ.BasisZ, center + radius * XYZ.BasisX);
            Line diameter = Line.CreateBound(arc.GetEndPoint(1), arc.GetEndPoint(0));
            var profile = new CurveLoop();
            profile.Append(arc);
            profile.Append(diameter);
            return GeometryCreationUtilities.CreateRevolvedGeometry(frame,
                new List<CurveLoop> { profile }, 0, 2 * Math.PI);
        }
    }
}
