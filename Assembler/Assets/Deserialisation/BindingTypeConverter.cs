using System;
using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	/// <summary>
	/// Reads a single <see cref="BindingDto"/>, which a descriptor may author two ways: as a bare control-path
	/// scalar (<c>"&lt;Keyboard&gt;/w"</c>) or as a composite mapping (<c>Composite: 2DVector</c> with
	/// Up/Down/Left/Right parts). Other keys in the mapping are treated as composite part names.
	/// </summary>
	internal class BindingTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(BindingDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			if (parser.TryConsume<Scalar>(out var scalar))
			{
				return new BindingDto { Path = scalar.Value };
			}

			parser.Consume<MappingStart>();

			string? composite = null;
			var parts = new Dictionary<string, string>();

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				var value = parser.Consume<Scalar>().Value;

				if (string.Equals(key, "Composite", StringComparison.OrdinalIgnoreCase))
				{
					composite = value;
				}
				else
				{
					parts[key] = value;
				}
			}

			return new BindingDto { Composite = composite, Parts = parts };
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
