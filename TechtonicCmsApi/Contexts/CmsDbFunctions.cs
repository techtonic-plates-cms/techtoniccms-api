using System.Text.Json;

namespace TechtonicCmsApi.Contexts;

/// <summary>
/// Static stub methods mapped to PostgreSQL <c>cms_extract_*</c> functions via EF Core
/// <see cref="Microsoft.EntityFrameworkCore.ModelBuilder.HasDbFunction"/>. These methods are
/// never executed at runtime — EF Core intercepts calls and translates them to SQL.
/// <para>
/// Usage in LINQ: <c>db.Entries.Where(e => CmsDbFunctions.CmsExtractText(e.Data, "title") == "Hello")</c>
/// Translates to: <c>WHERE cms_extract_text("Data", 'title') = 'Hello'</c>
/// </para>
/// </summary>
public static class CmsDbFunctions
{
    public static string? CmsExtractText(JsonDocument doc, string key)
        => throw new InvalidOperationException($"{nameof(CmsExtractText)} is a stub for EF Core DbFunction translation.");

    public static decimal? CmsExtractNumber(JsonDocument doc, string key)
        => throw new InvalidOperationException($"{nameof(CmsExtractNumber)} is a stub for EF Core DbFunction translation.");

    public static bool? CmsExtractBoolean(JsonDocument doc, string key)
        => throw new InvalidOperationException($"{nameof(CmsExtractBoolean)} is a stub for EF Core DbFunction translation.");

    public static DateTime? CmsExtractDateTime(JsonDocument doc, string key)
        => throw new InvalidOperationException($"{nameof(CmsExtractDateTime)} is a stub for EF Core DbFunction translation.");
}
