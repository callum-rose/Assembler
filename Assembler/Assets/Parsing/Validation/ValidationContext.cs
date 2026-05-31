using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Parsing.Info;

namespace Assembler.Parsing.Validation
{
	/// <summary>
	/// Accumulates <see cref="ValidationError"/>s during a single walk of the game tree and tracks
	/// the current location as a path. Validators record errors here instead of throwing, so one
	/// pass surfaces every problem. Reusable rule helpers live here so individual validators stay
	/// terse and emit consistent, fix-bearing messages.
	/// </summary>
	public sealed class ValidationContext
	{
		private readonly List<ValidationError> _errors = new();
		private readonly List<string> _path = new();

		public IReadOnlyList<ValidationError> Errors => _errors;

		/// <summary>Pushes a path segment for the duration of the returned scope.</summary>
		public IDisposable Scope(string segment)
		{
			_path.Add(segment);
			return new PopScope(this);
		}

		/// <summary>Records an error at the current path.</summary>
		public void Error(string problem, string fix) =>
			_errors.Add(new ValidationError(string.Join("/", _path), problem, fix));

		/// <summary>Fails when a required value source is absent (<see cref="None{T}"/>).</summary>
		public void RequireValue<T>(ValueSource<T> source, string property, string fix)
		{
			if (source is None<T>)
			{
				Error($"required value '{property}' is missing.", fix);
			}
		}

		/// <summary>Fails when a required string is null or empty.</summary>
		public void RequireNonEmpty(string? value, string name, string fix)
		{
			if (string.IsNullOrEmpty(value))
			{
				Error($"'{name}' is empty.", fix);
			}
		}

		/// <summary>Fails when a value is not one of the allowed options.</summary>
		public void RequireOneOf(string? value, string name, IReadOnlyCollection<string> allowed, string fix)
		{
			if (value is null || !allowed.Contains(value))
			{
				Error($"'{name}' is '{value}', which is not one of: {string.Join(", ", allowed)}.", fix);
			}
		}

		/// <summary>Records an error for each id that appears more than once.</summary>
		public void RequireUnique(IEnumerable<string> ids, string kind, string fix)
		{
			var seen = new HashSet<string>();

			foreach (var id in ids)
			{
				if (!seen.Add(id))
				{
					Error($"duplicate {kind} id '{id}'.", fix);
				}
			}
		}

		private sealed class PopScope : IDisposable
		{
			private readonly ValidationContext _ctx;

			public PopScope(ValidationContext ctx) => _ctx = ctx;

			public void Dispose() => _ctx._path.RemoveAt(_ctx._path.Count - 1);
		}
	}
}
