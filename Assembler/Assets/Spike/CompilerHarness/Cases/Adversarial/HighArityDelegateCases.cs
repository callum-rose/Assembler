using System;
using System.Text;
using Assembler.Compiler.Compiler;
using UnityEngine;

namespace Spike.CompilerHarness.Cases.Adversarial
{
	/// <summary>
	/// Adversarial family 2 — <b>high-arity delegate construction</b>. Drives
	/// <c>DelegateTypeHelper.GetDelegateType</c> across its full declared range, well past what any real
	/// descriptor uses, up to its <c>Action&lt;…16&gt;</c> / <c>Func&lt;…17&gt;</c> ceiling — and one step
	/// past, to confirm the ceiling is a clean positioned error rather than a crash.
	///
	/// <c>DelegateTypeHelper</c> hand-names every arity specifically so each path stays a closed
	/// <c>MakeGenericType</c> and never reaches <c>Expression.GetDelegateType</c>, which would emit a new
	/// delegate type via <c>Reflection.Emit</c> — unavailable under IL2CPP. That comment is the design
	/// claim; this family is the measurement of it. Every parameter is a <b>value type</b>, because a
	/// <c>Func&lt;Vector3, …×16, float&gt;</c> is a far larger AOT specialisation than the reference-type
	/// equivalent, which would share one canonical implementation.
	/// </summary>
	public static class HighArityDelegateCases
	{
		private const string Suite = "Adversarial/HighArityDelegate";

		// 1-4 walk the arities real descriptors actually use; 8/12/15/16 climb to the declared ceiling.
		private static readonly int[] Arities = { 1, 2, 3, 4, 8, 12, 15, 16 };

		public static void Register(SpikeCaseList list)
		{
			foreach (var arity in Arities)
			{
				var n = arity;

				list.Add($"{Suite}/FuncIntArity{n:00}", () =>
				{
					var compiler = new ExpressionMethodCompiler();
					var parameters = IntParameters(n);

					var func = compiler.Compile(SumBody(n), typeof(int), out var delegateType, parameters);

					Check.Equal(delegateType, ExpectedFuncType(typeof(int), n, typeof(int)), "delegate type");

					// Args 0..n-1, so the expected sum is the triangular number n(n-1)/2.
					Check.Equal(func.DynamicInvoke(AscendingIntArgs(n)), n * (n - 1) / 2);
				});

				list.Add($"{Suite}/FuncVector3Arity{n:00}", () =>
				{
					var compiler = new ExpressionMethodCompiler();
					var parameters = Vector3Parameters(n);

					var func = compiler.Compile(SumXBody(n), typeof(float), out var delegateType, parameters);

					Check.Equal(delegateType, ExpectedFuncType(typeof(Vector3), n, typeof(float)), "delegate type");

					var result = func.DynamicInvoke(AscendingVector3Args(n));
					Check.Approx(Convert.ToDouble(result), n * (n - 1) / 2.0, 1e-3, "sum of x components");
				});

				list.Add($"{Suite}/ActionIntArity{n:00}", () =>
				{
					var compiler = new ExpressionMethodCompiler();
					compiler.RegisterType(typeof(CoercionTarget), "CoercionTarget");
					var parameters = IntParameters(n);

					// A static field write is the only way to prove an Action actually ran — there is no
					// return value to assert on, and "it didn't throw" would pass even if the body were skipped.
					CoercionTarget.Shared = -1f;

					var action = compiler.Compile(
						$"CoercionTarget.Shared = {SumExpression(n)};",
						typeof(void),
						out var delegateType,
						parameters);

					Check.Equal(delegateType, ExpectedActionType(typeof(int), n), "delegate type");

					action.DynamicInvoke(AscendingIntArgs(n));
					Check.Approx(CoercionTarget.Shared, n * (n - 1) / 2.0, 1e-3, "CoercionTarget.Shared");
				});
			}

			list.Add($"{Suite}/FuncBeyondCeilingIsACleanError", () =>
			{
				// 17 parameters + a return type is 18 generic arguments — one past the largest Func<>.
				// DelegateTypeHelper throws NotSupportedException, which Compile re-wraps as a positioned
				// CompileException. The point is that the ceiling is a diagnosable error, not a hard crash.
				var compiler = new ExpressionMethodCompiler();
				Check.ThrowsCompile(
					() => compiler.Compile(SumBody(17), typeof(int), out _, IntParameters(17)),
					"Func beyond the arity ceiling");
			});

			list.Add($"{Suite}/ActionBeyondCeilingIsACleanError", () =>
			{
				var compiler = new ExpressionMethodCompiler();
				compiler.RegisterType(typeof(CoercionTarget), "CoercionTarget");
				Check.ThrowsCompile(
					() => compiler.Compile(
						$"CoercionTarget.Shared = {SumExpression(17)};",
						typeof(void),
						out _,
						IntParameters(17)),
					"Action beyond the arity ceiling");
			});
		}

		private static string SumExpression(int arity)
		{
			var builder = new StringBuilder();

			for (var i = 0; i < arity; i++)
			{
				if (i > 0)
				{
					builder.Append(" + ");
				}

				builder.Append($"p{i}");
			}

			return builder.ToString();
		}

		private static string SumBody(int arity) => $"return {SumExpression(arity)};";

		private static string SumXBody(int arity)
		{
			var builder = new StringBuilder("return ");

			for (var i = 0; i < arity; i++)
			{
				if (i > 0)
				{
					builder.Append(" + ");
				}

				builder.Append($"p{i}.x");
			}

			return builder.Append(';').ToString();
		}

		private static (Type type, string name)[] IntParameters(int arity) => Parameters(typeof(int), arity);

		private static (Type type, string name)[] Vector3Parameters(int arity) => Parameters(typeof(Vector3), arity);

		private static (Type type, string name)[] Parameters(Type type, int arity)
		{
			var parameters = new (Type type, string name)[arity];

			for (var i = 0; i < arity; i++)
			{
				parameters[i] = (type, $"p{i}");
			}

			return parameters;
		}

		private static object[] AscendingIntArgs(int arity)
		{
			var args = new object[arity];

			for (var i = 0; i < arity; i++)
			{
				args[i] = i;
			}

			return args;
		}

		private static object[] AscendingVector3Args(int arity)
		{
			var args = new object[arity];

			for (var i = 0; i < arity; i++)
			{
				args[i] = new Vector3(i, 0f, 0f);
			}

			return args;
		}

		// Mirrors DelegateTypeHelper's own mapping, so a case fails if the helper ever silently returns a
		// different (or dynamically emitted) delegate type than the one the arity should produce.
		private static Type ExpectedFuncType(Type parameterType, int arity, Type returnType)
		{
			var typeArguments = new Type[arity + 1];

			for (var i = 0; i < arity; i++)
			{
				typeArguments[i] = parameterType;
			}

			typeArguments[arity] = returnType;

			return OpenFuncType(arity + 1).MakeGenericType(typeArguments);
		}

		private static Type ExpectedActionType(Type parameterType, int arity)
		{
			var typeArguments = new Type[arity];

			for (var i = 0; i < arity; i++)
			{
				typeArguments[i] = parameterType;
			}

			return OpenActionType(arity).MakeGenericType(typeArguments);
		}

		private static Type OpenFuncType(int totalArguments) => totalArguments switch
		{
			1 => typeof(Func<>),
			2 => typeof(Func<,>),
			3 => typeof(Func<,,>),
			4 => typeof(Func<,,,>),
			5 => typeof(Func<,,,,>),
			6 => typeof(Func<,,,,,>),
			7 => typeof(Func<,,,,,,>),
			8 => typeof(Func<,,,,,,,>),
			9 => typeof(Func<,,,,,,,,>),
			10 => typeof(Func<,,,,,,,,,>),
			11 => typeof(Func<,,,,,,,,,,>),
			12 => typeof(Func<,,,,,,,,,,,>),
			13 => typeof(Func<,,,,,,,,,,,,>),
			14 => typeof(Func<,,,,,,,,,,,,,>),
			15 => typeof(Func<,,,,,,,,,,,,,,>),
			16 => typeof(Func<,,,,,,,,,,,,,,,>),
			17 => typeof(Func<,,,,,,,,,,,,,,,,>),
			_ => throw new NotSupportedException($"No Func<> with {totalArguments} type arguments.")
		};

		private static Type OpenActionType(int arity) => arity switch
		{
			1 => typeof(Action<>),
			2 => typeof(Action<,>),
			3 => typeof(Action<,,>),
			4 => typeof(Action<,,,>),
			5 => typeof(Action<,,,,>),
			6 => typeof(Action<,,,,,>),
			7 => typeof(Action<,,,,,,>),
			8 => typeof(Action<,,,,,,,>),
			9 => typeof(Action<,,,,,,,,>),
			10 => typeof(Action<,,,,,,,,,>),
			11 => typeof(Action<,,,,,,,,,,>),
			12 => typeof(Action<,,,,,,,,,,,>),
			13 => typeof(Action<,,,,,,,,,,,,>),
			14 => typeof(Action<,,,,,,,,,,,,,>),
			15 => typeof(Action<,,,,,,,,,,,,,,>),
			16 => typeof(Action<,,,,,,,,,,,,,,,>),
			_ => throw new NotSupportedException($"No Action<> with {arity} type arguments.")
		};
	}
}
