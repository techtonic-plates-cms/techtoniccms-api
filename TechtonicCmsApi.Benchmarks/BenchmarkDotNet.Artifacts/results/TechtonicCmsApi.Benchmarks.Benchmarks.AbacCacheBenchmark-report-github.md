```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat) (container)
11th Gen Intel Core i7-11800H 2.30GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.203
  [Host] : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Toolchain=InProcessEmitToolchain  InvocationCount=1  UnrollFactor=1  

```
| Method        | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|-------------- |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| AbacCacheHit  |  9.971 ms | 0.2720 ms | 0.7936 ms |  0.52 |    0.07 |    1 | 130.18 KB |        0.54 |
| AbacCacheMiss | 19.342 ms | 0.7338 ms | 2.1171 ms |  1.01 |    0.15 |    2 | 243.13 KB |        1.00 |
