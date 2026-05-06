```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat) (container)
11th Gen Intel Core i7-11800H 2.30GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.203
  [Host] : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Toolchain=InProcessEmitToolchain  InvocationCount=1  UnrollFactor=1  

```
| Method           | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| DenyFirst        | 15.03 ms | 0.342 ms | 1.009 ms |  1.00 |    0.10 |    1 | 311.54 KB |        1.00 |
| AllowAfterDenies | 14.97 ms | 0.376 ms | 1.091 ms |  1.00 |    0.10 |    1 | 311.67 KB |        1.00 |
