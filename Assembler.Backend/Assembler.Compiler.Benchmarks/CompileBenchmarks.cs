using BenchmarkDotNet.Attributes;

namespace Assembler.Compiler.Tests;

[MemoryDiagnoser]
public class CompileBenchmarks
{
	private ExpressionMethodCompiler _compiler = null!;

	[GlobalSetup]
	public void Setup()
	{
		_compiler = new ExpressionMethodCompiler();
		
		// Register some static methods for benchmarks that need them
		_compiler.RegisterStaticMethods(typeof(Math));
		_compiler.RegisterStaticMethods(typeof(string));
	}

	[Benchmark]
	public Func<int> CompileSimpleReturn()
	{
		return _compiler.CompileFunc<int>("return 42;");
	}

	[Benchmark]
	public Func<int, int> CompileSimpleParameter()
	{
		return _compiler.CompileFunc<int, int>("return x;", "x");
	}

	[Benchmark]
	public Func<int, int, int> CompileSimpleAddition()
	{
		var code = "return a + b;";
		var compiled = _compiler.Compile(code, typeof(int), (typeof(int), "a"), (typeof(int), "b"));
		return (Func<int, int, int>)compiled;
	}

	[Benchmark]
	public Func<int, int, int, int> CompileMultipleOperations()
	{
		var code = "return (a + b) * c;";
		var compiled = _compiler.Compile(code, typeof(int), (typeof(int), "a"), (typeof(int), "b"), (typeof(int), "c"));
		return (Func<int, int, int, int>)compiled;
	}

	[Benchmark]
	public Func<double, double, double> CompileMathOperation()
	{
		var code = "return Math.Pow(x, y);";
		var compiled = _compiler.Compile(code, typeof(double), (typeof(double), "x"), (typeof(double), "y"));
		return (Func<double, double, double>)compiled;
	}

	[Benchmark]
	public Action CompileSimpleAction()
	{
		return _compiler.CompileAction("var x = 42;");
	}

	[Benchmark]
	public Action<int> CompileActionWithParameter()
	{
		return _compiler.CompileAction<int>("var y = x + 10;", "x");
	}

	[Benchmark]
	public Func<bool, int> CompileConditional()
	{
		var code = "if (condition) { return 1; } else { return 0; }";
		var compiled = _compiler.Compile(code, typeof(int), (typeof(bool), "condition"));
		return (Func<bool, int>)compiled;
	}

	[Benchmark]
	public Func<int, int> CompileComplexExpression()
	{
		var code = "var temp = x * 2; var result = temp + 10; return result;";
		return _compiler.CompileFunc<int, int>(code, "x");
	}

	[Benchmark]
	public int CompileAndExecuteSimple()
	{
		var func = _compiler.CompileFunc<int>("return 42;");
		return func();
	}

	[Benchmark]
	public int CompileAndExecuteWithParameters()
	{
		var compiled = _compiler.Compile("return a + b;", typeof(int), (typeof(int), "a"), (typeof(int), "b"));
		var func = (Func<int, int, int>)compiled;
		return func(5, 10);
	}

	[Benchmark]
	public int CompileMultipleFunctions()
	{
		int sum = 0;
		for (int i = 0; i < 10; i++)
		{
			var func = _compiler.CompileFunc<int>($"return {i * 10};");
			sum += func();
		}
		return sum;
	}

	[Benchmark]
	public Func<string, int> CompileStringLength()
	{
		var code = "return str.Length;";
		var compiled = _compiler.Compile(code, typeof(int), (typeof(string), "str"));
		return (Func<string, int>)compiled;
	}

	[Benchmark]
	public Func<int, bool> CompileComparison()
	{
		var code = "return x > 10;";
		var compiled = _compiler.Compile(code, typeof(bool), (typeof(int), "x"));
		return (Func<int, bool>)compiled;
	}
}