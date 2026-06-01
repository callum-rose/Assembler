using System;
using System.Globalization;

namespace Assembler.Resolving
{
	/// <summary>
	/// Resolves a <c>!text</c> key to its localized template (via <see cref="StringTableRegistry"/>) and
	/// fills the template's <c>string.Format</c> placeholders with live argument values each time it is read.
	/// The template is fetched inside <see cref="Get"/> (a cheap dictionary lookup) so a future runtime
	/// locale change is reflected immediately. Read-only, like <see cref="ExpressionValueProvider{T}"/>.
	/// Generic over <typeparamref name="T"/> (always <c>string</c> or <c>object</c>) so a <c>!text</c> can
	/// also feed another source's <c>object</c>-typed argument list.
	/// </summary>
	public sealed class LocalizedTextProvider<T> : IValueProvider<T>
	{
		private readonly StringTableRegistry _table;
		private readonly string _key;
		private readonly IValueProvider[] _arguments;

		public LocalizedTextProvider(StringTableRegistry table, string key, IValueProvider[] arguments)
		{
			_table = table;
			_key = key;
			_arguments = arguments;
		}

		public T Get(TriggerContext ctx) => (T)(object)Format(ctx);

		object IValueProvider.Get(TriggerContext ctx) => Format(ctx);

		public void Set(T value) =>
			throw new InvalidOperationException("LocalizedTextProvider cannot have its value set");

		private string Format(TriggerContext ctx)
		{
			var template = _table.GetTemplate(_key);

			if (_arguments.Length == 0)
			{
				return template;
			}

			var args = new object[_arguments.Length];

			for (int i = 0; i < _arguments.Length; i++)
			{
				args[i] = _arguments[i].Get(ctx);
			}

			return string.Format(CultureInfo.CurrentCulture, template, args);
		}
	}
}
