using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using TaskExchange.Models;

namespace TaskExchange.Services
{
    public sealed class StorageService
    {
        private static readonly Guid AssignmentSchemaGuid = new Guid("A13A083C-B7E5-4E10-8B0E-94F704209A7D");
        private static readonly Guid LedgerSchemaGuid = new Guid("C537F81C-A2ED-4200-B005-029632CF493F");
        private const string JsonFieldName = "Json";
        private const string VersionFieldName = "FormatVersion";
        private readonly Document _document;

        public StorageService(Document document) => _document = document;

        public string CurrentModelId => _document.ProjectInformation?.UniqueId ?? _document.Title;

        public AssignmentSnapshot ReadAssignment(Element element)
        {
            if (element == null) return null;
            Schema schema = Schema.Lookup(AssignmentSchemaGuid);
            if (schema == null) return null;
            Entity entity = element.GetEntity(schema);
            if (!entity.IsValid()) return null;
            try { return JsonService.Deserialize<AssignmentSnapshot>(entity.Get<string>(schema.GetField(JsonFieldName))); }
            catch { return null; }
        }

        public void WriteAssignment(Element element, AssignmentSnapshot snapshot)
        {
            Schema schema = GetOrCreateSchema(AssignmentSchemaGuid, "UNI_TaskExchange_Assignment");
            var entity = new Entity(schema);
            entity.Set(schema.GetField(VersionFieldName), 1);
            entity.Set(schema.GetField(JsonFieldName), JsonService.Serialize(snapshot));
            element.SetEntity(entity);
        }

        public ProjectLedgerData ReadLedger()
        {
            Schema schema = Schema.Lookup(LedgerSchemaGuid);
            if (schema == null) return new ProjectLedgerData();
            DataStorage storage = FindLedgerStorage(schema);
            if (storage == null) return new ProjectLedgerData();
            try
            {
                Entity entity = storage.GetEntity(schema);
                return JsonService.Deserialize<ProjectLedgerData>(entity.Get<string>(schema.GetField(JsonFieldName)));
            }
            catch (Exception ex)
            {
                LogService.Error("Не удалось прочитать реестр заданий", ex);
                return new ProjectLedgerData();
            }
        }

        public void WriteLedger(ProjectLedgerData ledger)
        {
            Schema schema = GetOrCreateSchema(LedgerSchemaGuid, "UNI_TaskExchange_Ledger");
            DataStorage storage = FindLedgerStorage(schema) ?? DataStorage.Create(_document);
            storage.Name = "UNI Обмен заданиями";
            var entity = new Entity(schema);
            entity.Set(schema.GetField(VersionFieldName), 1);
            entity.Set(schema.GetField(JsonFieldName), JsonService.Serialize(ledger));
            storage.SetEntity(entity);
        }

        public void Upsert(ProjectLedgerData ledger, AssignmentSnapshot snapshot, string action)
        {
            int index = ledger.Assignments.FindIndex(x => string.Equals(x.Key, snapshot.Key, StringComparison.Ordinal));
            if (index >= 0) ledger.Assignments[index] = snapshot;
            else ledger.Assignments.Add(snapshot);

            ledger.History.Add(new HistoryEntry
            {
                AssignmentKey = snapshot.Key,
                Document = _document.Title,
                DateUtc = DateTime.UtcNow.ToString("O"),
                User = _document.Application.Username,
                Action = action,
                Parameters = string.Join("; ", snapshot.Parameters.Select(x => x.Key + "=" + ParameterCodec.Display(x.Value))),
                Location = $"X: {snapshot.X:0.###}; Y: {snapshot.Y:0.###}; Z: {snapshot.Z:0.###}",
                ElementUniqueId = snapshot.LocalElementUniqueId
            });
        }

        public IReadOnlyList<HistoryEntry> GetHistory(string assignmentKey) => ReadLedger().History
            .Where(x => string.Equals(x.AssignmentKey, assignmentKey, StringComparison.Ordinal))
            .OrderByDescending(x => x.DateUtc)
            .ToList();

        private DataStorage FindLedgerStorage(Schema schema) => new FilteredElementCollector(_document)
            .OfClass(typeof(DataStorage))
            .Cast<DataStorage>()
            .FirstOrDefault(x => x.GetEntity(schema).IsValid());

        private static Schema GetOrCreateSchema(Guid guid, string name)
        {
            Schema existing = Schema.Lookup(guid);
            if (existing != null) return existing;
            var builder = new SchemaBuilder(guid);
            builder.SetSchemaName(name);
            builder.SetDocumentation("Данные плагина обмена заданиями UNI");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(VersionFieldName, typeof(int));
            builder.AddSimpleField(JsonFieldName, typeof(string));
            return builder.Finish();
        }
    }
}
