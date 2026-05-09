using Assembler.Compiler.Tests;
using BenchmarkDotNet.Running;

// using Assembler.Compiler.Benchmarks;

// Run the comparison benchmarks between standard and fast expression compilers
// BenchmarkRunner.Run<CompilerComparisonBenchmarks>();

// Uncomment to run other benchmarks:

BenchmarkRunner.Run<CompiledBenchmarks>();
// BenchmarkRunner.Run<CompileBenchmarks>();
