using System.Reflection;
using System.Text.Json;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Data;

/// <summary>
/// Projects <see cref="Entry"/> rows (from EF Core) into dynamic CLR type instances
/// (emitted by <see cref="CmsTypeFactory"/>). Called by <see cref="CmsQueryable{T}"/>
/// during enumeration after the expression tree has been rewritten and executed against
/// the database.
///
/// Uses reflection (Activator.CreateInstance + PropertyInfo.SetValue) for simplicity.
/// Phase 6 may replace this with compiled expression delegates for performance.
///
/// All dynamic properties are nullable — if a JSONB key is missing or has a null value,
/// the property remains at its default (null/default).
/// </summary>
public static class CmsObjectMaterializer
{
    /// <summary>
    /// Cached PropertyInfo for Entry.Data, used to verify base field lookups
    /// don't accidentally collide with the JSONB column.
    /// </summary>
    private static readonly PropertyInfo EntryDataProperty = typeof(Entry).GetProperty(nameof(Entry.Data))!;

    /// <summary>
    /// Creates a new instance of <typeparamref name="TDynamic"/> and populates all properties
    /// from the given <paramref name="entry"/>.
    ///
    /// Base fields (Id, Name, Slug, etc.) are copied directly from Entry entity columns.
    /// Dynamic fields are extracted from Entry.Data (JSONB) using the field's JSON key
    /// and deserialized according to the field's <see cref="FieldDataType"/>.
    /// </summary>
    /// <typeparam name="TDynamic">The dynamic CLR type emitted by CmsTypeFactory.</typeparam>
    /// <param name="entry">The EF Core entity row to project from.</param>
    /// <param name="mappings">Ordered field mappings from CmsTypeFactory.GetFieldMappings().</param>
    /// <returns>A fully populated dynamic type instance.</returns>
    public static TDynamic Materialize<TDynamic>(Entry entry, CmsFieldMapping[] mappings)
    {
        var instance = (TDynamic)Activator.CreateInstance(typeof(TDynamic))!;

        foreach (var mapping in mappings)
        {
            // Skip properties that don't exist or aren't writable on the dynamic type.
            // This shouldn't happen since CmsTypeFactory emits all properties with setters,
            // but defensive coding in case of mismatched mapping data.
            var property = typeof(TDynamic).GetProperty(mapping.PropertyName);
            if (property is null || !property.CanWrite)
                continue;

            if (mapping.IsBaseField)
                SetBaseField(instance, property, entry);
            else
                SetDynamicField(instance, property, entry.Data, mapping);
        }

        return instance;
    }

    /// <summary>
    /// Copies a base field value directly from the Entry entity.
    /// Base fields (Id, Name, Slug, CreatedAt, UpdatedAt, PublishedAt, Status, Locale, CollectionId)
    /// map 1:1 to Entry entity columns, so we just read the matching property via reflection.
    /// </summary>
    private static void SetBaseField<T>(T instance, PropertyInfo property, Entry entry)
    {
        var entryProperty = typeof(Entry).GetProperty(property.Name);
        if (entryProperty is null)
            return;

        var value = entryProperty.GetValue(entry);
        property.SetValue(instance, value);
    }

    /// <summary>
    /// Extracts a dynamic field value from the Entry's JSONB data column.
    ///
    /// Looks up the field's JSON key in the JsonDocument's root element, then deserializes
    /// based on <see cref="FieldDataType"/>. Returns without setting the property if:
    ///   - The key doesn't exist in the JSONB object (field added after entry was created)
    ///   - The value is an explicit JSON null
    ///   - The value's JSON type doesn't match the expected type (type mismatch / corruption)
    ///
    /// Type extraction details:
    ///   - Text/RichText/Relation/Asset → element.GetString()
    ///   - Number → element.TryGetDouble() boxed as double? (nullable)
    ///   - Boolean → checked via ValueKind (True/False) since JsonElement.GetBoolean()
    ///     throws on non-boolean values; result boxed as bool?
    ///   - DateTime → element.TryGetDateTime() for ISO 8601 strings, boxed as DateTime?
    ///   - TextList → EnumerateArray + GetString() → string[]
    ///   - NumberList → EnumerateArray + GetDouble() → double[]
    ///   - Object → JsonDocument.Parse(element.GetRawText()) — clones the JSON subtree
    ///     so the new JsonDocument has independent ownership/lifecycle from the Entry's Data.
    /// </summary>
    private static void SetDynamicField<T>(
        T instance,
        PropertyInfo property,
        JsonDocument data,
        CmsFieldMapping mapping)
    {
        if (!data.RootElement.TryGetProperty(mapping.JsonKey, out var element))
            return;

        if (element.ValueKind == JsonValueKind.Null)
            return;

        object? value = mapping.DataType switch
        {
            // String-based fields: Text, RichText, Relation (Guid as string), Asset (id/path as string)
            FieldDataType.Text or FieldDataType.RichText or FieldDataType.Relation or FieldDataType.Asset
                => element.ValueKind == JsonValueKind.String ? element.GetString() : null,

            // Numeric field: double? to match CmsTypeFactory.MapFieldType and ExtractNumber's return type
            FieldDataType.Number
                => element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var d) ? d : null,

            // Boolean field: must check ValueKind explicitly (not GetBoolean which throws on non-bool)
            FieldDataType.Boolean
                => element.ValueKind == JsonValueKind.True ? true
                 : element.ValueKind == JsonValueKind.False ? false
                 : (object?)null,

            // DateTime field: ISO 8601 string in JSON, parsed to DateTime?
            FieldDataType.DateTime
                => element.ValueKind == JsonValueKind.String && element.TryGetDateTime(out var dt) ? dt : null,

            // TextList: JSON array of strings → string[]
            FieldDataType.TextList
                => element.ValueKind == JsonValueKind.Array
                    ? element.EnumerateArray().Select(e => e.GetString()).ToArray() as object
                    : null,

            // NumberList: JSON array of numbers → double[]
            FieldDataType.NumberList
                => element.ValueKind == JsonValueKind.Array
                    ? element.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.Number)
                        .Select(e => e.GetDouble())
                        .ToArray() as object
                    : null,

            // Object: arbitrary JSON object → cloned JsonDocument for safe ownership.
            // The clone is necessary because Entry.Data (and its JsonElements) belongs to the
            // Entry's JsonDocument, which may be disposed. Parsing GetRawText() creates an
            // independent copy. Note: the dynamic type's Object property has no disposal mechanism,
            // so these JsonDocuments rely on GC finalization — acceptable for Phase 3, to be
            // addressed in Phase 6 with pooling or IDisposable on dynamic types.
            FieldDataType.Object
                => element.ValueKind == JsonValueKind.Object
                    ? JsonDocument.Parse(element.GetRawText())
                    : null,

            _ => null
        };

        property.SetValue(instance, value);
    }
}
