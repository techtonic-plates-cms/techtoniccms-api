```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat) (container)
11th Gen Intel Core i7-11800H 2.30GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.203
  [Host] : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Toolchain=InProcessEmitToolchain  InvocationCount=1  UnrollFactor=1  

```
| Method       | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------- |---------:|---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| WithAudit    | 32.55 ms | 1.178 ms | 3.436 ms | 32.20 ms |  1.01 |    0.15 |    2 | 412.09 KB |        1.00 |
| WithoutAudit | 22.19 ms | 2.255 ms | 6.650 ms | 18.56 ms |  0.69 |    0.22 |    1 | 451.23 KB |        1.09 |
