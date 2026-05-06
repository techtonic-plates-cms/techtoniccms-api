```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat) (container)
11th Gen Intel Core i7-11800H 2.30GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.203
  [Host] : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Toolchain=InProcessEmitToolchain  InvocationCount=1  UnrollFactor=1  

```
| Method       | PolicyCount | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------- |------------ |----------:|----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| **WithCache**    | **1**           |  **5.643 ms** | **0.1687 ms** | **0.4895 ms** |  **5.623 ms** |  **1.01** |    **0.12** |    **1** | **138.58 KB** |        **1.00** |
| WithoutCache | 1           | 14.589 ms | 0.3432 ms | 1.0012 ms | 14.610 ms |  2.60 |    0.28 |    2 | 256.24 KB |        1.85 |
|              |             |           |           |           |           |       |         |      |           |             |
| **WithCache**    | **5**           |  **5.868 ms** | **0.2017 ms** | **0.5947 ms** |  **5.740 ms** |  **1.01** |    **0.14** |    **1** | **148.55 KB** |        **1.00** |
| WithoutCache | 5           | 14.673 ms | 0.4209 ms | 1.2277 ms | 14.653 ms |  2.53 |    0.32 |    2 | 278.69 KB |        1.88 |
|              |             |           |           |           |           |       |         |      |           |             |
| **WithCache**    | **10**          |  **5.963 ms** | **0.2239 ms** | **0.6566 ms** |  **5.940 ms** |  **1.01** |    **0.16** |    **1** | **161.68 KB** |        **1.00** |
| WithoutCache | 10          | 14.462 ms | 0.3214 ms | 0.9272 ms | 14.505 ms |  2.45 |    0.31 |    2 | 306.43 KB |        1.90 |
|              |             |           |           |           |           |       |         |      |           |             |
| **WithCache**    | **25**          |  **6.112 ms** | **0.1741 ms** | **0.5051 ms** |  **6.138 ms** |  **1.01** |    **0.12** |    **1** | **199.11 KB** |        **1.00** |
| WithoutCache | 25          | 16.447 ms | 0.8871 ms | 2.6157 ms | 15.650 ms |  2.71 |    0.49 |    2 | 388.63 KB |        1.95 |
|              |             |           |           |           |           |       |         |      |           |             |
| **WithCache**    | **50**          |  **6.449 ms** | **0.1847 ms** | **0.5446 ms** |  **6.481 ms** |  **1.01** |    **0.12** |    **1** | **264.09 KB** |        **1.00** |
| WithoutCache | 50          | 16.572 ms | 0.3628 ms | 1.0641 ms | 16.540 ms |  2.59 |    0.28 |    2 | 528.99 KB |        2.00 |
