using System;
using System.Globalization;
using System.Linq;

namespace Assembler.Resolving
{
	/// <summary>
	/// Resolves a <c>!text</c> key to its localised template (via <see cref="StringTableRegistry"/>) and
	/// fills the template's <c>string.Format</c> placeholders with live argument values each time it is read.
	/// The template is fetched inside <see cref="Get"/> (a cheap dictionary lookup) so a future runtime
	/// locale change is reflected immediately. Read-only, like <see cref="ExpressionValueProvider{T}"/>.
	/// A <c>!text</c> always yields a string, so this provider is concretely <see cref="IValueProvider{T}"/>
	/// of <c>string</c> rather than being generic.
	/// </summary>
	public sealed class LocalisedTextProvider : IValueProvider<string>
	{
		private readonly StringTableRegistry _table;
		private readonly string _key;
		private readonly IValueProvider[] _arguments;

		public LocalisedTextProvider(StringTableRegistry table, string key, IValueProvider[] arguments)
		{
			_table = table;
			_key = key;
			_arguments = arguments;
		}

		public string Get(TriggerContext ctx) => Format(ctx);

		object IValueProvider.Get(TriggerContext ctx) => Format(ctx);

		public void Set(string value) =>
			throw new InvalidOperationException("LocalisedTextProvider cannot have its value set");

		private string Format(TriggerContext ctx)
		{
			var template = _table.GetTemplate(_key);

			if (_arguments.Length == 0)
			{
				return template;
			}

			var args = _arguments.Select(arg => arg.Get(ctx)).ToArray();

			return string.Format(CultureInfo.CurrentCulture, template, args);
		}
	}
}
