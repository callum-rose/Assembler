using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Assembler.Compiler.Tests;

public class CompiledBenchmarks
{
	private Func<int, int, int> _adder;
	private Func<int> _whileLoop;
	private Func<int, int> _fibonnacci;

	[GlobalSetup]
	public void Setup()
	{
		var compiler = new ExpressionMethodCompiler();

		_adder = compiler.CompileFunc<int, int, int>("return a + b;", "a", "b");

		_whileLoop = compiler.CompileFunc<int>(
			"""
			int sum = 0;

			for (int i = 0; i < 1000; i++)
			{
				sum += i;
			}

			return sum;
			""");

		_fibonnacci = compiler.CompileFunc<int, int>(
			"""
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
			""",
			"n");
	}

	[Benchmark(Baseline = true)]
	public int CSharpAdder()
	{
		return Add(1, 2);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Add(int a, int b) => a + b;
	}

	[Benchmark]
	public int FuncAdder()
	{
		return _adder(1, 2);
	}

	[Benchmark]
	public int CSharpWhileLoop()
	{
		int sum = 0;

		for (int i = 0; i < 1000; i++)
		{
			sum += i;
		}

		return sum;
	}

	[Benchmark]
	public int FuncWhileLoop()
	{
		return _whileLoop();
	}

	[Benchmark]
	public int CSharpFibonacci()
	{
		return Fibonacci(100);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static int Fibonacci(int n)
		{
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
		}
	}

	[Benchmark]
	public int FuncFibonacci()
	{
		return _fibonnacci(100);
	}
}