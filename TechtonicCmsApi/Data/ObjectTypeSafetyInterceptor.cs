using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Data;

public class ObjectTypeSafetyInterceptor : SaveChangesInterceptor
{
    // Keyed by Collection Id, containing the Field definitions for that collection
    private readonly Dictionary<Guid, List<Field>> _fieldSchemas;

    public ObjectTypeSafetyInterceptor(Dictionary<Guid, List<Field>> fieldSchemas)
    {
        _fieldSchemas = fieldSchemas;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, 
        InterceptionResult<int> result, 
        CancellationToken cancellationToken = default)
    {
        // Watch for changes on the Entry table
        var entries = eventData.Context?.ChangeTracker.Entries<Entry>() 
                      ?? Enumerable.Empty<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Entry>>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                ValidateObjectData(entry.Entity);
            }
        }

        return ValueTask.FromResult(result);
    }

    private void ValidateObjectData(Entry entry)
    {
        // Access RootElement of the JsonDocument
        if (entry.Data.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new DbUpdateException($"Data for entry '{entry.Id}' must be a valid JSON object.");
        }

        // Look up schema using the Entry's CollectionId (Guid)
        if (!_fieldSchemas.TryGetValue(entry.CollectionId, out var fields))
        {
            throw new DbUpdateException($"No field schema found for Collection ID '{entry.CollectionId}'.");
        }

        foreach (var jsonProp in entry.Data.RootElement.EnumerateObject())
        {
            // Match against the Field entity's "Name" property (e.g., "title", "views")
            var fieldDef = fields.FirstOrDefault(f => f.Name == jsonProp.Name);
            
            if (fieldDef == null)
            {
                throw new DbUpdateException(
                    $"Field '{jsonProp.Name}' is not defined in the schema for Collection ID '{entry.CollectionId}'.");
            }

            // Allow nulls if the field isn't strictly required
            if (jsonProp.Value.ValueKind == JsonValueKind.Null && !fieldDef.IsRequired) 
                continue;

            bool isValid = fieldDef.DataType switch
            {
                FieldDataType.Text or FieldDataType.RichText => jsonProp.Value.ValueKind == JsonValueKind.String,
                
                // Number supports both int and float in JSON
                FieldDataType.Number => jsonProp.Value.ValueKind == JsonValueKind.Number && jsonProp.Value.TryGetDouble(out _),
                
                FieldDataType.Boolean => jsonProp.Value.ValueKind == JsonValueKind.True || jsonProp.Value.ValueKind == JsonValueKind.False,
                
                // DateTime must be a valid ISO string
                FieldDataType.DateTime => jsonProp.Value.ValueKind == JsonValueKind.String && DateTime.TryParse(jsonProp.Value.GetString(), out _),
                
                // Relation stores a Guid string in the JSON
                FieldDataType.Relation => jsonProp.Value.ValueKind == JsonValueKind.String && Guid.TryParse(jsonProp.Value.GetString(), out _),
                
                // TextList: Array of strings
                FieldDataType.TextList => jsonProp.Value.ValueKind == JsonValueKind.Array && jsonProp.Value.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String),
                
                // NumberList: Array of numbers
                FieldDataType.NumberList => jsonProp.Value.ValueKind == JsonValueKind.Array && jsonProp.Value.EnumerateArray().All(x => x.ValueKind == JsonValueKind.Number),
                
                // Asset and Object: Must be a JSON object block
                FieldDataType.Asset or FieldDataType.Object => jsonProp.Value.ValueKind == JsonValueKind.Object,
                
                _ => true 
            };

            if (!isValid)
            {
                throw new DbUpdateException(
                    $"Type violation in Entry '{entry.Id}' (Collection ID '{entry.CollectionId}'), Field '{fieldDef.Name}'. " +
                    $"Expected JSON format for '{fieldDef.DataType}', but received '{jsonProp.Value.ValueKind}'.");
            }
        }
    }
}