```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26100.3775/24H2/2024Update/HudsonValley)
12th Gen Intel Core i7-12700KF 3.61GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 8.0.405
  [Host]     : .NET 8.0.21 (8.0.21, 8.0.2125.47513), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 8.0.21 (8.0.21, 8.0.2125.47513), X64 RyuJIT x86-64-v3


```
| Method          | Mean        | Error     | StdDev    | Ratio  | RatioSD |
|---------------- |------------:|----------:|----------:|-------:|--------:|
| CSharpAdder     |   0.4830 ns | 0.0494 ns | 0.0986 ns |   1.04 |    0.31 |
| FuncAdder       |   0.6875 ns | 0.0529 ns | 0.0707 ns |   1.48 |    0.35 |
| CSharpWhileLoop | 258.1777 ns | 0.7794 ns | 0.9572 ns | 557.59 |  117.41 |
| FuncWhileLoop   | 282.3436 ns | 2.1025 ns | 1.9667 ns | 609.78 |  128.47 |
| CSharpFibonacci |  36.0115 ns | 0.2108 ns | 0.1972 ns |  77.77 |   16.38 |
| FuncFibonacci   |  31.4673 ns | 0.1876 ns | 0.1663 ns |  67.96 |   14.32 |
