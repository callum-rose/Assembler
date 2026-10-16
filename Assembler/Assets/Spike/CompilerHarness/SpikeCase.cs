using System;
using System.Collections.Generic;

namespace Spike.CompilerHarness
{
	/// <summary>
	/// One AOT verdict. Deliberately not an NUnit test: NUnit itself does non-trivial reflection and
	/// generic instantiation under IL2CPP, so a failure inside it would be indistinguishable from a
	/// failure of the thing we are actually measuring — the expression compiler.
	/// </summary>
	public sealed class SpikeCase
	{
		public SpikeCase(string id, Action run)
		{
			Id = id;
			Run = run;
		}

		/// <summary>Stable <c>Suite/Name</c> identifier, logged before the case executes so a hard crash still names it.</summary>
		public string Id { get; }

		/// <summary>Compiles and invokes the case, throwing <see cref="SpikeAssertException"/> on a wrong answer.</summary>
		public Action Run { get; }
	}

	/// <summary>Accumulator the per-suite case files register into, keeping the corpus one flat ordered list.</summary>
	public sealed class SpikeCaseList
	{
		private readonly List<SpikeCase> _cases = new();

		public IReadOnlyList<SpikeCase> Cases => _cases;

		public void Add(string id, Action run) => _cases.Add(new SpikeCase(id, run));
	}

	/// <summary>Thrown by <see cref="Check"/> when a case produces the wrong answer rather than crashing.</summary>
	public sealed class SpikeAssertException : Exception
	{
		public SpikeAssertException(string message) : base(message)
		{
		}
	}
}
