using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using TaskExchange.Services;

namespace TaskExchange
{
    public sealed class App : IExternalApplication
    {
        private static TaskManagerSession _session;
        private int _closingDocumentId = -1;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                RibbonPanel panel = application.CreateRibbonPanel("Обмен заданиями");
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                var data = new PushButtonData(
                    "TaskExchange.Open",
                    "Задания",
                    assemblyPath,
                    typeof(OpenTaskManagerCommand).FullName)
                {
                    ToolTip = "Открыть менеджер заданий из связанных моделей",
                    LongDescription = "Проверяет наличие семейства UNI_Задание и открывает менеджер получения и обновления заданий."
                };
                var button = panel.AddItem(data) as PushButton;
                if (button != null)
                {
                    button.Image = CreateIcon(16);
                    button.LargeImage = CreateIcon(32);
                }
                application.ControlledApplication.DocumentClosing += DocumentClosing;
                application.ControlledApplication.DocumentClosed += DocumentClosed;
                application.ApplicationClosing += ApplicationClosing;
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Обмен заданиями", "Не удалось создать панель плагина.\n\n" + ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            application.ControlledApplication.DocumentClosing -= DocumentClosing;
            application.ControlledApplication.DocumentClosed -= DocumentClosed;
            application.ApplicationClosing -= ApplicationClosing;
            _session?.Close();
            return Result.Succeeded;
        }

        internal static void ShowManager(UIApplication application, string familyName)
        {
            if (_session != null && !_session.IsClosed)
            {
                if (_session.IsFor(application.ActiveUIDocument?.Document))
                {
                    _session.Window.Activate();
                    return;
                }
                _session.Close();
            }
            _session = new TaskManagerSession(application, familyName,
                closed => { if (ReferenceEquals(_session, closed)) _session = null; });
        }

        private void DocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            _closingDocumentId = _session != null && _session.IsFor(e.Document) ? e.DocumentId : -1;
        }

        private void DocumentClosed(object sender, DocumentClosedEventArgs e)
        {
            if (_closingDocumentId == e.DocumentId && e.Status == RevitAPIEventStatus.Succeeded)
                _session?.Close();
            _closingDocumentId = -1;
        }

        private void ApplicationClosing(object sender, ApplicationClosingEventArgs e) => _session?.Close();

        private static BitmapSource CreateIcon(int size)
        {
            var visual = new DrawingVisual();
            using (DrawingContext drawing = visual.RenderOpen())
            {
                drawing.PushTransform(new ScaleTransform(size / 32.0, size / 32.0));
                var blue = new SolidColorBrush(Color.FromRgb(45, 105, 151));
                var white = Brushes.White;
                drawing.DrawRoundedRectangle(blue, null, new Rect(1, 1, 30, 30), 5, 5);
                drawing.DrawRoundedRectangle(white, null, new Rect(7, 5, 18, 22), 2, 2);
                var line = new Pen(blue, 2.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                drawing.DrawLine(line, new Point(10, 11), new Point(12, 13));
                drawing.DrawLine(line, new Point(12, 13), new Point(15, 9));
                drawing.DrawLine(line, new Point(10, 18), new Point(12, 20));
                drawing.DrawLine(line, new Point(12, 20), new Point(15, 16));
                drawing.DrawLine(line, new Point(17, 12), new Point(22, 12));
                drawing.DrawLine(line, new Point(17, 19), new Point(22, 19));
                drawing.Pop();
            }
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
