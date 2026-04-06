using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace TechtonicCmsApi.Data;

/// <summary>
/// EF Core stubs that translate to PostgreSQL function calls during query compilation.
/// These methods are never actually executed — EF Core intercepts calls to them
/// and replaces them with the corresponding SQL function in the generated query.
/// 
/// The PostgreSQL functions are created by the initial migration (Up method).
/// 
/// Usage in LINQ:
///   var results = await db.Entries
///       .Where(e => CmsObjectFunctions.ExtractText(e.Data, "title") == "Hello")
///       .ToListAsync();
/// 
/// Generates SQL:
///   SELECT ... FROM entries WHERE cms_extract_text(data, 'title') = 'Hello'
/// </summary>
public static class CmsObjectFunctions
{
    /// <summary>
    /// Extracts a text value from a JSONB document by key.
    /// Maps to PostgreSQL's built-in jsonb_extract_path_text (no custom function needed).
    /// Used for: Text, RichText, Relation, Asset fields.
    /// </summary>
    [DbFunction("jsonb_extract_path_text", IsBuiltIn = true)]
    public static string? JsonExtractPathText(JsonDocument json, params string[] path)
        => throw new NotSupportedException();

    /// <summary>
    /// Extracts a text value from a JSONB document by key.
    /// Maps to cms_extract_text(doc jsonb, key text) → text
    /// Returns NULL if the key doesn't exist (STRICT function).
    /// </summary>
    [DbFunction("cms_extract_text", IsBuiltIn = false)]
    public static string? ExtractText(JsonDocument json, string key)
        => throw new NotSupportedException();

    /// <summary>
    /// Extracts a numeric value from a JSONB document by key.
    /// Maps to cms_extract_number(doc jsonb, key text) → numeric
    /// Returns NULL if the key doesn't exist or value isn't numeric.
    /// </summary>
    [DbFunction("cms_extract_number", IsBuiltIn = false)]
    public static double? ExtractNumber(JsonDocument json, string key)
        => throw new NotSupportedException();

    /// <summary>
    /// Extracts a boolean value from a JSONB document by key.
    /// Maps to cms_extract_boolean(doc jsonb, key text) → boolean
    /// Returns NULL if the key doesn't exist or value isn't boolean.
    /// </summary>
    [DbFunction("cms_extract_boolean", IsBuiltIn = false)]
    public static bool? ExtractBoolean(JsonDocument json, string key)
        => throw new NotSupportedException();

    /// <summary>
    /// Extracts a timestamp value from a JSONB document by key.
    /// Maps to cms_extract_datetime(doc jsonb, key text) → timestamptz
    /// Returns NULL if the key doesn't exist or value isn't a valid timestamp.
    /// </summary>
    [DbFunction("cms_extract_datetime", IsBuiltIn = false)]
    public static DateTime? ExtractDateTime(JsonDocument json, string key)
        => throw new NotSupportedException();
}
