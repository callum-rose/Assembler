using System;

namespace Assembler.Resolving
{
	/// <summary>
	/// Adapts any <see cref="IValueProvider"/> into an <see cref="IValueProvider{T}"/> of
	/// <see cref="string"/> by calling <c>ToString()</c> on the inner value each time it
	/// is read. This allows behaviours whose property is typed as a string (e.g.
	/// <c>text label.Text</c>) to live-bind to numeric, vector or boolean variables
	/// without needing an explicit cast in user yaml.
	/// </summary>
	public sealed class StringifyingValueProvider : IValueProvider<string>
	{
		private readonly IValueProvider _inner;

		public StringifyingValueProvider(IValueProvider inner)
		{
			_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		}

		public string Value
		{
			get
			{
				var v = _inner.Value;
				return v?.ToString() ?? string.Empty;
			}
			set => throw new InvalidOperationException(
				"StringifyingValueProvider is read-only — the inner value cannot be set through the string view.");
		}

		object IValueProvider.Value => Value;
	}
}
