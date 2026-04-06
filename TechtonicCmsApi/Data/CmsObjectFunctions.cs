using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace TechtonicCmsApi.Data;

public static class CmsObjectFunctions
{
    [DbFunction("jsonb_extract_path_text", IsBuiltIn = true)]
    public static string? JsonExtractPathText(JsonDocument json, params string[] path) => throw new NotSupportedException();

      [DbFunction("(data->>?)::numeric", IsBuiltIn = false)]
    public static double JsonNumeric(JsonElement element, string path) => throw new NotSupportedException();

    [DbFunction("(data->>?)::boolean", IsBuiltIn = false)]
    public static bool JsonBool(JsonElement element, string path) => throw new NotSupportedException();

    [DbFunction("(data->>?)::timestamp", IsBuiltIn = false)]
    public static DateTime JsonDateTime(JsonElement element, string path) => throw new NotSupportedException();
}