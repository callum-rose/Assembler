```

BenchmarkDotNet v0.15.6, macOS 26.3.1 (25D2128) [Darwin 25.3.0]
Apple M3, 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), Arm64 RyuJIT armv8.0-a


```
| Method                                 | Mean           | Error       | StdDev      | Gen0   | Gen1   | Gen2   | Allocated |
|--------------------------------------- |---------------:|------------:|------------:|-------:|-------:|-------:|----------:|
| &#39;Standard: Compile Simple Addition&#39;    | 12,975.9219 ns |  91.5007 ns |  85.5898 ns | 2.1973 | 1.0986 | 0.0305 |   18408 B |
| &#39;Fast: Compile Simple Addition&#39;        |  4,605.1730 ns |  19.4846 ns |  17.2726 ns | 1.7548 | 0.8774 | 0.0458 |   14728 B |
| &#39;Standard: Compile Complex Expression&#39; | 18,630.4589 ns | 134.8172 ns | 126.1081 ns | 3.2349 | 1.5869 |      - |   27065 B |
| &#39;Fast: Compile Complex Expression&#39;     |  6,197.6994 ns |  17.6406 ns |  16.5011 ns | 2.5558 | 0.8469 | 0.0534 |   21370 B |
| &#39;Standard: Compile With Conditionals&#39;  | 23,954.7503 ns |  90.7442 ns |  75.7755 ns | 3.4180 | 1.7090 |      - |   28744 B |
| &#39;Fast: Compile With Conditionals&#39;      |  6,384.4628 ns |  20.8107 ns |  18.4482 ns | 2.9373 | 0.9766 | 0.0610 |   24616 B |
| &#39;Standard: Compile With Loop&#39;          | 29,912.9901 ns | 396.7957 ns | 331.3424 ns | 3.6621 | 1.8311 |      - |   31408 B |
| &#39;Fast: Compile With Loop&#39;              |  6,702.9290 ns |  94.0437 ns |  87.9685 ns | 2.8687 | 0.9537 | 0.0534 |   24016 B |
| &#39;Standard: Compile Fibonacci&#39;          | 44,416.9082 ns | 450.9807 ns | 352.0961 ns | 5.7373 | 0.7324 |      - |   48129 B |
| &#39;Fast: Compile Fibonacci&#39;              | 10,030.2320 ns | 197.5935 ns | 313.4042 ns | 4.3640 | 1.4496 | 0.0610 |   36497 B |
| &#39;Standard: Compile Math Operation&#39;     |             NA |          NA |          NA |     NA |     NA |     NA |        NA |
| &#39;Fast: Compile Math Operation&#39;         |             NA |          NA |          NA |     NA |     NA |     NA |        NA |
| &#39;Standard: Execute Simple Addition&#39;    |      0.3236 ns |   0.0024 ns |   0.0020 ns |      - |      - |      - |         - |
| &#39;Fast: Execute Simple Addition&#39;        |      0.3681 ns |   0.0096 ns |   0.0080 ns |      - |      - |      - |         - |
| &#39;Standard: Execute Fibonacci(20)&#39;      |      5.7259 ns |   0.0825 ns |   0.0771 ns |      - |      - |      - |         - |
| &#39;Fast: Execute Fibonacci(20)&#39;          |      5.9758 ns |   0.0462 ns |   0.0409 ns |      - |      - |      - |         - |
| &#39;Standard: Compile + Execute Addition&#39; | 13,290.6419 ns | 256.0067 ns | 273.9243 ns | 2.1973 | 1.0986 | 0.0305 |   18408 B |
| &#39;Fast: Compile + Execute Addition&#39;     | 12,137.6181 ns | 238.8745 ns | 245.3065 ns | 1.7395 | 0.8545 | 0.0305 |   14728 B |
| &#39;Standard: Compile + Execute Complex&#39;  | 31,445.5090 ns | 162.2496 ns | 143.8301 ns | 4.0283 | 1.9531 |      - |   34688 B |
| &#39;Fast: Compile + Execute Complex&#39;      | 28,438.1935 ns | 465.6220 ns | 388.8155 ns | 3.2349 | 1.0376 |      - |   27243 B |

Benchmarks with issues:
  CompilerComparisonBenchmarks.'Standard: Compile Math Operation': DefaultJob
  CompilerComparisonBenchmarks.'Fast: Compile Math Operation': DefaultJob
