```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat) (container)
11th Gen Intel Core i7-11800H 2.30GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.203
  [Host] : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Toolchain=InProcessEmitToolchain  

```
| Method            | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |---------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| BaselineQuery     | 1.125 ms | 0.0222 ms | 0.0537 ms |  1.00 |    0.07 |    1 | 3.9063 |  60.07 KB |        1.00 |
| UnrestrictedQuery | 5.695 ms | 0.1122 ms | 0.2189 ms |  5.07 |    0.31 |    3 | 7.8125 |  144.5 KB |        2.41 |
| RestrictedQuery   | 1.259 ms | 0.0250 ms | 0.0619 ms |  1.12 |    0.08 |    2 | 3.9063 |  61.29 KB |        1.02 |
