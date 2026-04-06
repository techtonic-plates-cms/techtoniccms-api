using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Data;

public class ObjectTypeSafetyInterceptor : SaveChangesInterceptor
{
    private readonly Dictionary<Guid, List<Field>> _fieldSchemas;

    public ObjectTypeSafetyInterceptor(Dictionary<Guid, List<Field>> fieldSchemas)
    {
        _fieldSchemas = fieldSchemas;
    }

    public ValueTask<InterceptionResult<Guid>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<Guid> result, CancellationToken cancellationToken = default)
    {
        var entries = eventData.Context?.ChangeTracker.Entries<Collection>() ?? Enumerable.Empty<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Collection>>();
        foreach (var entry in entries) {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                
            }
        }

        return ValueTask.FromResult(result);
    }


    private void ValidateObjectData(Entry entry)
    {
        if (entry.Data.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Data for entry '{entry}' must be a JSON object.");
        }

        if (!_fieldSchemas.TryGetValue(entry.CollectionId, out var fields))
        {
            throw new InvalidOperationException($"No field schema found for collection ID '{entry.CollectionId}'.");
        }

        foreach (var jsonProp in entry.Data.RootElement.EnumerateObject())
        {
            var fieldDef = fields.FirstOrDefault(f => f.Name == jsonProp.Name);
            if (fieldDef == null)
            {
                throw new InvalidOperationException($"Field '{jsonProp.Name}' is not defined in the schema for collection ID '{entry.CollectionId}'.");
            }

            if (jsonProp.Value.ValueKind == JsonValueKind.Null && !fieldDef.IsRequired) continue;

            bool isValid = fieldDef.DataType switch
            {
                   FieldDataType.String => jsonProp.Value.ValueKind == JsonValueKind.String,
                FieldDataType.Integer => jsonProp.Value.ValueKind == JsonValueKind.Number && jsonProp.Value.TryGetInt32(out _),
                FieldDataType.Float => jsonProp.Value.ValueKind == JsonValueKind.Number,
                FieldType.Boolean => jsonProp.Value.ValueKind == JsonValueKind.True || jsonProp.Value.ValueKind == JsonValueKind.False,
                FieldType.DateTime => jsonProp.Value.ValueKind == JsonValueKind.String && DateTime.TryParse(jsonProp.Value.GetString(), out _),
                _ => true
            };
            // Additional type checks can be implemented here based on field.Type
        }
    }
}