using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Data;

/// <summary>
/// Generates CLR types at runtime using System.Reflection.Emit, one per CMS Collection.
/// Each emitted type is a POCO with:
///   - 9 base properties mapping to Entry entity columns (Id, Name, Slug, etc.)
///   - N dynamic properties mapping to keys inside Entry.Data (JSONB), derived from Field definitions.
/// 
/// These types are consumed by:
///   - CmsTypeModule (Phase 4) — registers them as HotChocolate ObjectTypes with filtering/sorting/paging
///   - CmsExpressionRewriter (Phase 3) — translates property accesses into JSONB function calls
///   - CmsObjectMaterializer (Phase 3) — instantiates and populates them from Entry rows
/// 
/// Lifecycle:
///   1. On startup, CmsTypeModule.CreateTypesAsync calls GetOrCreateType for every collection
///   2. On schema change (admin modifies a Collection/Field), InvalidateAll is called,
///      caches are cleared, and the next CreateTypesAsync rebuilds everything from fresh DB state
///   3. Old types remain in memory (assemblies cannot be unloaded in .NET) but are no longer referenced
/// </summary>
public sealed class CmsTypeFactory
{
    // In-memory assembly and module that hold all emitted dynamic types.
    // Created once; all types are defined within the same module.
    // AssemblyBuilderAccess.Run = executable in memory, not saved to disk.
    private readonly AssemblyBuilder _assemblyBuilder;
    private readonly ModuleBuilder _moduleBuilder;

    // Cache of emitted CLR types keyed by Collection.Id.
    // ConcurrentDictionary for thread-safety during parallel schema rebuilds.
    private readonly ConcurrentDictionary<Guid, Type> _typeCache = new();

    // Metadata for each property on each dynamic type, used by CmsExpressionRewriter
    // to know how to translate LINQ property accesses into JSONB function calls.
    private readonly ConcurrentDictionary<Guid, List<CmsFieldMapping>> _fieldMappings = new();

    // Base properties present on every dynamic type, derived from the Entry entity columns.
    // These map 1:1 to Entry entity properties — the expression rewriter passes them through
    // directly to Entry.PropertyName rather than rewriting to JSONB.
    // Format: (C# property name, CLR type, JSONB key placeholder — unused for base fields)
    private static readonly (string Name, Type ClrType, string JsonKey)[] BaseFields =
    [
        ("Id", typeof(Guid), "id"),
        ("Name", typeof(string), "name"),
        ("Slug", typeof(string), "slug"),
        ("CreatedAt", typeof(DateTime), "created_at"),
        ("UpdatedAt", typeof(DateTime), "updated_at"),
        ("PublishedAt", typeof(DateTime?), "published_at"),
        ("Status", typeof(EntryStatus), "status"),
        ("Locale", typeof(Locale), "locale"),
        ("CollectionId", typeof(Guid), "collection_id"),
    ];

    public CmsTypeFactory()
    {
        _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("TechtonicCmsApi.Dynamic"),
            AssemblyBuilderAccess.Run);

        _moduleBuilder = _assemblyBuilder.DefineDynamicModule("DynamicTypes");
    }

    /// <summary>
    /// Returns the emitted CLR type for a collection, creating it on first access.
    /// Thread-safe via ConcurrentDictionary.GetOrAdd.
    /// </summary>
    public Type GetOrCreateType(Collection collection, List<Field> fields)
    {
        return _typeCache.GetOrAdd(collection.Id, _ => BuildType(collection, fields));
    }

    /// <summary>
    /// Returns the ordered field mappings for a collection's dynamic type.
    /// Used by CmsExpressionRewriter to translate property accesses.
    /// </summary>
    public IReadOnlyList<CmsFieldMapping> GetFieldMappings(Guid collectionId)
    {
        return _fieldMappings.TryGetValue(collectionId, out var mappings)
            ? mappings
            : Array.Empty<CmsFieldMapping>();
    }

    /// <summary>
    /// Clears all caches, forcing full regeneration on next GetOrCreateType calls.
    /// Called during schema rebuild when Collections or Fields are modified.
    /// </summary>
    public void InvalidateAll()
    {
        _typeCache.Clear();
        _fieldMappings.Clear();
    }

    /// <summary>
    /// Emits a complete CLR type for a collection.
    /// 
    /// For a collection with slug "blog-posts" and fields [title (Text), views (Number)],
    /// this generates the equivalent of:
    /// 
    ///   namespace TechtonicCmsApi.Dynamic {
    ///     public sealed class BlogPostsEntry {
    ///       public Guid Id { get; set; }
    ///       public string Name { get; set; }
    ///       // ... 7 more base properties ...
    ///       public string Title { get; set; }     // from Field: title (Text)
    ///       public double? Views { get; set; }    // from Field: views (Number)
    ///     }
    ///   }
    /// </summary>
    private Type BuildType(Collection collection, List<Field> fields)
    {
        // Type name: slug "blog-posts" → "BlogPostsEntry"
        var typeName = $"{ToPascalCase(collection.Slug)}Entry";

        // Define an empty type skeleton — properties are added via EmitProperty below.
        // Sealed = slight optimization since these types are never subclassed.
        var typeBuilder = _moduleBuilder.DefineType(
            $"TechtonicCmsApi.Dynamic.{typeName}",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);

        var mappings = new List<CmsFieldMapping>();

        // Emit the 9 base properties (Id, Name, Slug, etc.)
        foreach (var (name, clrType, jsonKey) in BaseFields)
        {
            EmitProperty(typeBuilder, name, clrType, mappings, jsonKey, isBaseField: true, dataType: null);
        }

        // Emit dynamic properties from Field definitions, ordered by name for deterministic output.
        // Field.Name is used as both the C# property name and the JSONB key (case-sensitive).
        foreach (var field in fields.OrderBy(f => f.Name))
        {
            var clrType = MapFieldType(field.DataType);
            EmitProperty(typeBuilder, field.Name, clrType, mappings, field.Name, isBaseField: false, dataType: field.DataType);
        }

        // Bake the type into the assembly — after this call no more members can be added.
        var type = typeBuilder.CreateType()!;
        _fieldMappings[collection.Id] = mappings;
        return type;
    }

    /// <summary>
    /// Emits a single property (backing field + getter + setter) onto a TypeBuilder using raw IL.
    /// 
    /// This generates the equivalent of:
    ///   private {Type} _{propertyName};
    ///   public {Type} {propertyName} {
    ///     get => _{propertyName};
    ///     set => _{propertyName} = value;
    ///   }
    /// 
    /// IL breakdown for getter (3 instructions):
    ///   Ldarg_0  — load 'this' onto the evaluation stack
    ///   Ldfld    — load the value of the backing field from 'this'
    ///   Ret      — return the loaded value
    /// 
    /// IL breakdown for setter (3 instructions):
    ///   Ldarg_0  — load 'this'
    ///   Ldarg_1  — load the 'value' argument
    ///   Stfld    — store the value into the backing field on 'this'
    ///   Ret      — return
    /// </summary>
    private static void EmitProperty(
        TypeBuilder typeBuilder,
        string propertyName,
        Type propertyType,
        List<CmsFieldMapping> mappings,
        string jsonKey,
        bool isBaseField,
        FieldDataType? dataType)
    {
        // Private backing field: e.g., "_title"
        var backingField = typeBuilder.DefineField(
            $"_{char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}",
            propertyType,
            FieldAttributes.Private);

        var propertyBuilder = typeBuilder.DefineProperty(
            propertyName,
            PropertyAttributes.HasDefault,
            propertyType,
            null);

        // Getter method: get_{PropertyName}
        var getter = typeBuilder.DefineMethod(
            $"get_{propertyName}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            propertyType,
            Type.EmptyTypes);

        var getIl = getter.GetILGenerator();
        getIl.Emit(OpCodes.Ldarg_0);      // load 'this'
        getIl.Emit(OpCodes.Ldfld, backingField); // load field value
        getIl.Emit(OpCodes.Ret);           // return it

        propertyBuilder.SetGetMethod(getter);

        // Setter method: set_{PropertyName}
        var setter = typeBuilder.DefineMethod(
            $"set_{propertyName}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null,
            [propertyType]);

        var setIl = setter.GetILGenerator();
        setIl.Emit(OpCodes.Ldarg_0);       // load 'this'
        setIl.Emit(OpCodes.Ldarg_1);       // load 'value' argument
        setIl.Emit(OpCodes.Stfld, backingField); // store into field
        setIl.Emit(OpCodes.Ret);            // return

        propertyBuilder.SetSetMethod(setter);

        // Record metadata so CmsExpressionRewriter knows how to handle this property.
        // IsBaseField=true → rewrite to Entry.PropertyName (column access)
        // IsBaseField=false → rewrite to JSONB extraction function on Entry.Data
        mappings.Add(new CmsFieldMapping(propertyName, jsonKey, propertyType, isBaseField, dataType));
    }

    /// <summary>
    /// Maps a CMS FieldDataType enum to a CLR type for the emitted property.
    /// 
    /// All types are nullable — JSONB values are inherently optional at the PostgreSQL level,
    /// so even IsRequired fields could be absent before validation runs. HotChocolate's
    /// GraphQL schema can still mark fields as non-null based on the IsRequired metadata.
    /// 
    /// Relation and Asset store Guid strings in JSONB (not Guid structs) because
    /// JSON serialization of Guids varies by client and string is more portable.
    /// Object fields use JsonDocument for unstructured/nested JSON.
    /// </summary>
    private static Type MapFieldType(FieldDataType dataType)
    {
        return dataType switch
        {
            FieldDataType.Text => typeof(string),
            FieldDataType.RichText => typeof(string),
            FieldDataType.Relation => typeof(string),
            FieldDataType.Asset => typeof(string),
            FieldDataType.Number => typeof(double?),
            FieldDataType.Boolean => typeof(bool?),
            FieldDataType.DateTime => typeof(DateTime?),
            FieldDataType.TextList => typeof(string[]),
            FieldDataType.NumberList => typeof(double[]),
            FieldDataType.Object => typeof(JsonDocument),
            _ => typeof(string)
        };
    }

    /// <summary>
    /// Converts a kebab-case or snake_case collection slug to PascalCase for type naming.
    /// E.g., "blog-posts" → "BlogPosts", "user_profiles" → "UserProfiles"
    /// </summary>
    private static string ToPascalCase(string slug)
    {
        return string.Concat(slug
            .Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(static s => char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant()));
    }
}
