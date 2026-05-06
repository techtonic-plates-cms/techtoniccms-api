```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat) (container)
11th Gen Intel Core i7-11800H 2.30GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.203
  [Host] : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Toolchain=InProcessEmitToolchain  

```
| Method                | EntryCount | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0      | Gen1      | Gen2      | Allocated   | Alloc Ratio |
|---------------------- |----------- |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|----------:|----------:|----------:|------------:|------------:|
| **WithPredicatePushdown** | **100**        |   **1.144 ms** | **0.0225 ms** | **0.0536 ms** |   **1.139 ms** |  **1.00** |    **0.07** |    **1** |    **3.9063** |         **-** |         **-** |    **63.71 KB** |        **1.00** |
| WithInMemoryFilter    | 100        |   2.074 ms | 0.0415 ms | 0.0994 ms |   2.060 ms |  1.82 |    0.12 |    2 |   50.7813 |   19.5313 |         - |   666.77 KB |       10.47 |
|                       |            |            |           |           |            |       |         |      |           |           |           |             |             |
| **WithPredicatePushdown** | **1000**       |   **1.609 ms** | **0.0318 ms** | **0.0589 ms** |   **1.602 ms** |  **1.00** |    **0.05** |    **1** |    **3.9063** |         **-** |         **-** |    **63.72 KB** |        **1.00** |
| WithInMemoryFilter    | 1000       |  12.290 ms | 0.2474 ms | 0.7295 ms |  12.241 ms |  7.65 |    0.53 |    2 |  500.0000 |  484.3750 |         - |  6146.55 KB |       96.46 |
|                       |            |            |           |           |            |       |         |      |           |           |           |             |             |
| **WithPredicatePushdown** | **10000**      |   **5.890 ms** | **0.2634 ms** | **0.7765 ms** |   **5.588 ms** |  **1.02** |    **0.18** |    **1** |         **-** |         **-** |         **-** |    **63.79 KB** |        **1.00** |
| WithInMemoryFilter    | 10000      | 197.841 ms | 3.6157 ms | 6.9662 ms | 198.167 ms | 34.12 |    4.23 |    2 | 6000.0000 | 5666.6667 | 1333.3333 | 60971.45 KB |      955.89 |
