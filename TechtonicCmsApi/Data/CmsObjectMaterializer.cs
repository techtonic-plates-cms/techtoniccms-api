using System.Reflection;
using System.Text.Json;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Data;

public static class CmsObjectMaterializer
{
    private static readonly PropertyInfo EntryDataProperty = typeof(Entry).GetProperty(nameof(Entry.Data))!;

    public static TDynamic Materialize<TDynamic>(Entry entry, CmsFieldMapping[] mappings)
    {
        var instance = (TDynamic)Activator.CreateInstance(typeof(TDynamic))!;

        foreach (var mapping in mappings)
        {
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

    private static void SetBaseField<T>(T instance, PropertyInfo property, Entry entry)
    {
        var entryProperty = typeof(Entry).GetProperty(property.Name);
        if (entryProperty is null)
            return;

        var value = entryProperty.GetValue(entry);
        property.SetValue(instance, value);
    }

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
            FieldDataType.Text or FieldDataType.RichText or FieldDataType.Relation or FieldDataType.Asset
                => element.ValueKind == JsonValueKind.String ? element.GetString() : null,

            FieldDataType.Number
                => element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var d) ? d : null,

            FieldDataType.Boolean
                => element.ValueKind == JsonValueKind.True ? true
                 : element.ValueKind == JsonValueKind.False ? false
                 : (object?)null,

            FieldDataType.DateTime
                => element.ValueKind == JsonValueKind.String && element.TryGetDateTime(out var dt) ? dt : null,

            FieldDataType.TextList
                => element.ValueKind == JsonValueKind.Array
                    ? element.EnumerateArray().Select(e => e.GetString()).ToArray() as object
                    : null,

            FieldDataType.NumberList
                => element.ValueKind == JsonValueKind.Array
                    ? element.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.Number)
                        .Select(e => e.GetDouble())
                        .ToArray() as object
                    : null,

            FieldDataType.Object
                => element.ValueKind == JsonValueKind.Object
                    ? JsonDocument.Parse(element.GetRawText())
                    : null,

            _ => null
        };

        property.SetValue(instance, value);
    }
}
