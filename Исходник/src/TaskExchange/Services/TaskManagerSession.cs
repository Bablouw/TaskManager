using System;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TaskExchange.UI;

namespace TaskExchange.Services
{
    internal sealed class TaskManagerSession : IDisposable
    {
        private readonly RevitRequestDispatcher _dispatcher;
        private readonly Action<TaskManagerSession> _onClosed;
        private bool _closed;

        public Document Document { get; }
        public ManagerWindow Window { get; }
        public bool IsClosed => _closed;

        public TaskManagerSession(UIApplication application, string familyName, Action<TaskManagerSession> onClosed)
        {
            Document = application.ActiveUIDocument.Document;
            _onClosed = onClosed;
            _dispatcher = new RevitRequestDispatcher();
            try
            {
                Window = new ManagerWindow(application, this, familyName);
                Window.Closed += (s, e) => Close();
                Window.Show();
            }
            catch
            {
                _dispatcher.Dispose();
                throw;
            }
        }

        public bool IsFor(Document document) =>
            document != null && (ReferenceEquals(Document, document) || Document.Equals(document));

        public void Post(Action<UIApplication> action)
        {
            if (_closed) return;
            _dispatcher.Post(application =>
            {
                if (_closed || !Document.IsValidObject) return;
                if (!IsFor(application.ActiveUIDocument?.Document))
                {
                    MessageBox.Show(Window,
                        "Вернитесь к проекту, в котором открыт менеджер заданий.", "Обмен заданиями");
                    return;
                }
                action(application);
            });
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true;
            if (Window.IsLoaded) Window.Close();
            if (Document.IsValidObject)
                _dispatcher.PostThenDispose(MovePreviewService.Clear);
            else
            {
                MovePreviewService.Drop();
                _dispatcher.Dispose();
            }
            _onClosed?.Invoke(this);
        }

        public void Dispose() => Close();
    }
}
