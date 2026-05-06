using Microsoft.AspNetCore.Http;

namespace TechtonicCmsApi.Benchmarks.Infrastructure;

/// <summary>
/// Minimal IHttpContextAccessor for benchmarks where HTTP context is not available.
/// </summary>
public class FakeHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
