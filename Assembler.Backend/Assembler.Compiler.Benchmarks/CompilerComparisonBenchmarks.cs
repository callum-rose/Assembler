using BenchmarkDotNet.Attributes;

namespace Assembler.Compiler.Benchmarks;

/// <summary>
/// Benchmarks comparing ExpressionMethodCompiler (using standard .NET Compile())
/// vs FastExpressionMethodCompiler (using FastExpressionCompiler's CompileFast())
/// for both compilation speed and execution speed of compiled expressions.
/// </summary>
[MemoryDiagnoser]
public class CompilerComparisonBenchmarks
{
	private ExpressionMethodCompiler _standardCompiler = null!;
	private FastExpressionMethodCompiler _fastCompiler = null!;

	// Pre-compiled delegates for execution benchmarks
	private Func<int, int, int> _standardAdder = null!;
	private Func<int, int, int> _fastAdder = null!;

	private Func<int, int> _standardFibonacci = null!;
	private Func<int, int> _fastFibonacci = null!;

	// private Func<double, double, double> _standardMathOp = null!;
	// private Func<double, double, double> _fastMathOp = null!;

	[GlobalSetup]
	public void Setup()
	{
		_standardCompiler = new ExpressionMethodCompiler();
		_fastCompiler = new FastExpressionMethodCompiler();

		// Register Math type and its static methods for both compilers
		_standardCompiler.RegisterType(typeof(Math));
		_standardCompiler.RegisterStaticMethods(typeof(Math));
		_fastCompiler.RegisterType(typeof(Math));
		_fastCompiler.RegisterStaticMethods(typeof(Math));

		// Pre-compile delegates for execution benchmarks
		var returnAB = "return a + b;";
		
		_standardAdder = _standardCompiler.CompileFunc<int, int, int>(returnAB, "a", "b");
		_fastAdder = _fastCompiler.CompileFunc<int, int, int>(returnAB, "a", "b");

		var fibonacciCode = """
		                    if (n <= 1)
		                    {
		                    	return n;
		                    }

		                    int a = 0;
		                    int b = 1;

		                    for (int i = 2; i <= n; i++)
		                    {
		                    	int temp = a + b;
		                    	a = b;
		                    	b = temp;
		                    }

		                    return b;
		                    """;

		_standardFibonacci = _standardCompiler.CompileFunc<int, int>(fibonacciCode, "n");
		_fastFibonacci = _fastCompiler.CompileFunc<int, int>(fibonacciCode, "n");

		// var returnMathPowXY = "return Math.Pow(x, y);";
		// _standardMathOp = _standardCompiler.CompileFunc<double, double, double>(returnMathPowXY, "x", "y");
		// _fastMathOp = _fastCompiler.CompileFunc<double, double, double>(returnMathPowXY, "x", "y");
	}

#region Compilation Speed Benchmarks

	[Benchmark(Description = "Standard: Compile Simple Addition")]
	public Func<int, int, int> StandardCompileSimpleAddition()
	{
		return _standardCompiler.CompileFunc<int, int, int>("return a + b;", "a", "b");
	}

	[Benchmark(Description = "Fast: Compile Simple Addition")]
	public Func<int, int, int> FastCompileSimpleAddition()
	{
		return _fastCompiler.CompileFunc<int, int, int>("return a + b;", "a", "b");
	}

	[Benchmark(Description = "Standard: Compile Complex Expression")]
	public Delegate StandardCompileComplexExpression()
	{
		var code = "var temp = (a + b) * c; var result = temp / 2; return result;";
		return _standardCompiler.Compile(code, typeof(int), (typeof(int), "a"), (typeof(int), "b"), (typeof(int), "c"));
	}

	[Benchmark(Description = "Fast: Compile Complex Expression")]
	public Delegate FastCompileComplexExpression()
	{
		var code = "var temp = (a + b) * c; var result = temp / 2; return result;";
		return _fastCompiler.Compile(code, typeof(int), (typeof(int), "a"), (typeof(int), "b"), (typeof(int), "c"));
	}

	[Benchmark(Description = "Standard: Compile With Conditionals")]
	public Func<int, int> StandardCompileConditional()
	{
		var code = """
		           if (x > 100)
		           {
		           	return x * 2;
		           }
		           else if (x > 50)
		           {
		           	return x + 10;
		           }
		           else
		           {
		           	return x;
		           }
		           """;
		return _standardCompiler.CompileFunc<int, int>(code, "x");
	}

	[Benchmark(Description = "Fast: Compile With Conditionals")]
	public Func<int, int> FastCompileConditional()
	{
		var code = """
		           if (x > 100)
		           {
		           	return x * 2;
		           }
		           else if (x > 50)
		           {
		           	return x + 10;
		           }
		           else
		           {
		           	return x;
		           }
		           """;
		return _fastCompiler.CompileFunc<int, int>(code, "x");
	}

	[Benchmark(Description = "Standard: Compile With Loop")]
	public Func<int, int> StandardCompileLoop()
	{
		var code = """
		           int sum = 0;
		           for (int i = 0; i < n; i++)
		           {
		           	sum += i;
		           }
		           return sum;
		           """;
		return _standardCompiler.CompileFunc<int, int>(code, "n");
	}

	[Benchmark(Description = "Fast: Compile With Loop")]
	public Func<int, int> FastCompileLoop()
	{
		var code = """
		           int sum = 0;
		           for (int i = 0; i < n; i++)
		           {
		           	sum += i;
		           }
		           return sum;
		           """;
		return _fastCompiler.CompileFunc<int, int>(code, "n");
	}

	[Benchmark(Description = "Standard: Compile Fibonacci")]
	public Func<int, int> StandardCompileFibonacci()
	{
		var code = """
		           if (n <= 1)
		           {
		           	return n;
		           }

		           int a = 0;
		           int b = 1;

		           for (int i = 2; i <= n; i++)
		           {
		           	int temp = a + b;
		           	a = b;
		           	b = temp;
		           }

		           return b;
		           """;
		return _standardCompiler.CompileFunc<int, int>(code, "n");
	}

	[Benchmark(Description = "Fast: Compile Fibonacci")]
	public Func<int, int> FastCompileFibonacci()
	{
		var code = """
		           if (n <= 1)
		           {
		           	return n;
		           }

		           int a = 0;
		           int b = 1;

		           for (int i = 2; i <= n; i++)
		           {
		           	int temp = a + b;
		           	a = b;
		           	b = temp;
		           }

		           return b;
		           """;
		return _fastCompiler.CompileFunc<int, int>(code, "n");
	}

	[Benchmark(Description = "Standard: Compile Math Operation")]
	public Func<double, double, double> StandardCompileMathOperation()
	{
		return _standardCompiler.CompileFunc<double, double, double>("return Math.Pow(x, y);", "x", "y");
	}

	[Benchmark(Description = "Fast: Compile Math Operation")]
	public Func<double, double, double> FastCompileMathOperation()
	{
		return _fastCompiler.CompileFunc<double, double, double>("return Math.Pow(x, y);", "x", "y");
	}

#endregion

#region Execution Speed Benchmarks

	[Benchmark(Description = "Standard: Execute Simple Addition")]
	public int StandardExecuteSimpleAddition()
	{
		return _standardAdder(5, 10);
	}

	[Benchmark(Description = "Fast: Execute Simple Addition")]
	public int FastExecuteSimpleAddition()
	{
		return _fastAdder(5, 10);
	}

	[Benchmark(Description = "Standard: Execute Fibonacci(20)")]
	public int StandardExecuteFibonacci()
	{
		return _standardFibonacci(20);
	}

	[Benchmark(Description = "Fast: Execute Fibonacci(20)")]
	public int FastExecuteFibonacci()
	{
		return _fastFibonacci(20);
	}

	// [Benchmark(Description = "Standard: Execute Math.Pow")]
	// public double StandardExecuteMathOperation()
	// {
	// 	return _standardMathOp(2.5, 3.5);
	// }
	//
	// [Benchmark(Description = "Fast: Execute Math.Pow")]
	// public double FastExecuteMathOperation()
	// {
	// 	return _fastMathOp(2.5, 3.5);
	// }

#endregion

#region Combined Compile + Execute Benchmarks

	[Benchmark(Description = "Standard: Compile + Execute Addition")]
	public int StandardCompileAndExecuteAddition()
	{
		var func = _standardCompiler.CompileFunc<int, int, int>("return a + b;", "a", "b");
		return func(5, 10);
	}

	[Benchmark(Description = "Fast: Compile + Execute Addition")]
	public int FastCompileAndExecuteAddition()
	{
		var func = _fastCompiler.CompileFunc<int, int, int>("return a + b;", "a", "b");
		return func(5, 10);
	}

	[Benchmark(Description = "Standard: Compile + Execute Complex")]
	public int StandardCompileAndExecuteComplex()
	{
		var code = """
		           int result = 0;
		           for (int i = 0; i < 100; i++)
		           {
		           	if (i % 2 == 0)
		           	{
		           		result += i;
		           	}
		           }
		           return result;
		           """;
		var func = _standardCompiler.CompileFunc<int>(code);
		return func();
	}

	[Benchmark(Description = "Fast: Compile + Execute Complex")]
	public int FastCompileAndExecuteComplex()
	{
		var code = """
		           int result = 0;
		           for (int i = 0; i < 100; i++)
		           {
		           	if (i % 2 == 0)
		           	{
		           		result += i;
		           	}
		           }
		           return result;
		           """;
		var func = _fastCompiler.CompileFunc<int>(code);
		return func();
	}

#endregion
}