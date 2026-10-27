using System;
using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.UI;

namespace TaskExchange.Services
{
    // Modeless WPF callbacks must not call the Revit API directly.
    internal sealed class RevitRequestDispatcher : IExternalEventHandler, IDisposable
    {
        private readonly Queue<Action<UIApplication>> _requests = new Queue<Action<UIApplication>>();
        private readonly ExternalEvent _externalEvent;
        private bool _disposed;
        private bool _disposeWhenDrained;

        public RevitRequestDispatcher() => _externalEvent = ExternalEvent.Create(this);

        public void Post(Action<UIApplication> request)
        {
            if (_disposed) return;
            _requests.Enqueue(request);
            Raise();
        }

        public void PostThenDispose(Action<UIApplication> request)
        {
            if (_disposed) return;
            _requests.Clear();
            _requests.Enqueue(request);
            _disposeWhenDrained = true;
            Raise();
        }

        private void Raise()
        {
            ExternalEventRequest result = _externalEvent.Raise();
            if (result == ExternalEventRequest.Denied || result == ExternalEventRequest.TimedOut)
            {
                _requests.Clear();
                if (_disposeWhenDrained) Dispose();
                MessageBox.Show("Revit не принял запрос плагина. Повторите действие после завершения текущей команды.",
                    "Обмен заданиями");
            }
        }

        public void Execute(UIApplication application)
        {
            while (!_disposed && _requests.Count > 0)
            {
                Action<UIApplication> request = _requests.Dequeue();
                try { request(application); }
                catch (Exception ex)
                {
                    LogService.Error("Ошибка действия из немодального окна", ex);
                    MessageBox.Show(ex.Message, "Обмен заданиями");
                }
            }
            if (_disposeWhenDrained) Dispose();
        }

        public string GetName() => "UNI TaskExchange modeless requests";

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _requests.Clear();
            _externalEvent.Dispose();
        }
    }
}
