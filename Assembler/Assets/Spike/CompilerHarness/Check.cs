using System;
using System.Collections;
using System.Text;
using Assembler.Compiler.Compiler;

namespace Spike.CompilerHarness
{
	/// <summary>
	/// The bespoke assert used instead of NUnit. Every method here is deliberately <b>non-generic</b>:
	/// a generic assert would instantiate <c>EqualityComparer&lt;T&gt;</c> and friends per value type,
	/// which is itself an IL2CPP generic-instantiation risk. If the harness's own asserts could fail
	/// under AOT, a red case would no longer be unambiguously the compiler's fault — which is the whole
	/// point of this spike. Boxing through <see cref="object"/> keeps the harness side boring.
	/// </summary>
	public static class Check
	{
		/// <summary>Value equality via <see cref="object.Equals(object)"/>. Pass matching runtime types — <c>Equal(1, 1.0)</c> fails.</summary>
		public static void Equal(object? actual, object? expected, string what = "value")
		{
			if (!Equals(actual, expected))
			{
				throw Fail($"{what}: expected {Describe(expected)}, got {Describe(actual)}");
			}
		}

		/// <summary>Floating-point equality within a tolerance, mirroring NUnit's <c>Within(...)</c>.</summary>
		public static void Approx(double actual, double expected, double tolerance, string what = "value")
		{
			if (double.IsNaN(actual) || Math.Abs(actual - expected) > tolerance)
			{
				throw Fail($"{what}: expected {expected} +/- {tolerance}, got {actual}");
			}
		}

		public static void True(bool condition, string what)
		{
			if (!condition)
			{
				throw Fail($"{what}: expected true, got false");
			}
		}

		public static void False(bool condition, string what)
		{
			if (condition)
			{
				throw Fail($"{what}: expected false, got true");
			}
		}

		public static void NotNull(object? value, string what)
		{
			if (value is null)
			{
				throw Fail($"{what}: expected non-null");
			}
		}

		public static void IsInstanceOf(Type expected, object? value, string what)
		{
			if (value is null || !expected.IsInstanceOfType(value))
			{
				throw Fail($"{what}: expected an instance of {expected.Name}, got {Describe(value)}");
			}
		}

		/// <summary>Element-wise comparison over the non-generic <see cref="IEnumerable"/> view, so no comparer is instantiated.</summary>
		public static void Sequence(IEnumerable? actual, IEnumerable expected, string what = "sequence")
		{
			if (actual is null)
			{
				throw Fail($"{what}: expected a sequence, got null");
			}

			var actualEnumerator = actual.GetEnumerator();
			var expectedEnumerator = expected.GetEnumerator();
			var index = 0;

			while (true)
			{
				var actualNext = actualEnumerator.MoveNext();
				var expectedNext = expectedEnumerator.MoveNext();

				if (!actualNext && !expectedNext)
				{
					return;
				}

				if (actualNext != expectedNext)
				{
					throw Fail($"{what}: length mismatch at index {index} " +
						$"(actual {(actualNext ? "has more" : "ended")}, expected {(expectedNext ? "has more" : "ended")})");
				}

				if (!Equals(actualEnumerator.Current, expectedEnumerator.Current))
				{
					throw Fail($"{what}[{index}]: expected {Describe(expectedEnumerator.Current)}, " +
						$"got {Describe(actualEnumerator.Current)}");
				}

				index++;
			}
		}

		public static void IsEmpty(IEnumerable? actual, string what = "sequence")
		{
			if (actual is null)
			{
				throw Fail($"{what}: expected an empty sequence, got null");
			}

			if (actual.GetEnumerator().MoveNext())
			{
				throw Fail($"{what}: expected an empty sequence, got at least one element");
			}
		}

		public static void Contains(string? haystack, string needle, string what)
		{
			if (haystack is null || !haystack.Contains(needle))
			{
				throw Fail($"{what}: expected to contain '{needle}', got {Describe(haystack)}");
			}
		}

		public static void DoesNotContain(string? haystack, string needle, string what)
		{
			if (haystack is not null && haystack.Contains(needle))
			{
				throw Fail($"{what}: expected NOT to contain '{needle}', got {Describe(haystack)}");
			}
		}

		public static void Greater(double actual, double threshold, string what)
		{
			if (!(actual > threshold))
			{
				throw Fail($"{what}: expected > {threshold}, got {actual}");
			}
		}

		/// <summary>
		/// Asserts the action raises a positioned <see cref="CompileException"/> and returns it for
		/// message/line inspection. Non-generic because every negative case in the corpus expects this
		/// one exception type.
		/// </summary>
		public static CompileException ThrowsCompile(Action action, string what)
		{
			try
			{
				action();
			}
			catch (CompileException ex)
			{
				return ex;
			}
			catch (Exception ex)
			{
				throw Fail($"{what}: expected a CompileException, got {ex.GetType().Name}: {ex.Message}");
			}

			throw Fail($"{what}: expected a CompileException, but nothing was thrown");
		}

		public static void DoesNotThrow(Action action, string what)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				throw Fail($"{what}: expected no exception, got {ex.GetType().Name}: {ex.Message}");
			}
		}

		private static SpikeAssertException Fail(string message) => new(message);

		private static string Describe(object? value) => value switch
		{
			null => "null",
			string s => $"\"{s}\"",
			IEnumerable enumerable and not string => DescribeSequence(enumerable),
			_ => value.ToString() ?? "?"
		};

		private static string DescribeSequence(IEnumerable enumerable)
		{
			var builder = new StringBuilder("[");
			var first = true;

			foreach (var item in enumerable)
			{
				if (!first)
				{
					builder.Append(", ");
				}

				builder.Append(item?.ToString() ?? "null");
				first = false;
			}

			return builder.Append(']').ToString();
		}
	}
}
