using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TaskExchange.Services;
using TaskExchange.UI;

namespace TaskExchange
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class OpenTaskManagerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;
                if (uiDocument?.Document == null)
                {
                    message = "Нет открытого проекта Revit.";
                    return Result.Failed;
                }

                var familyService = new FamilyService(uiDocument.Document);
                FamilyCheckResult family = familyService.EnsureFamilyLoaded();
                if (!family.Success)
                {
                    TaskDialog.Show("Обмен заданиями", family.Message);
                    return Result.Failed;
                }

                App.ShowManager(commandData.Application, family.FamilyName);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                LogService.Error("Ошибка открытия менеджера", ex);
                message = ex.Message;
                TaskDialog.Show("Обмен заданиями", "Не удалось открыть менеджер.\n\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
