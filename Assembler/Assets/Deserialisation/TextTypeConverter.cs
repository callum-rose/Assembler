using System;
using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	internal class TextTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(TextRefDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			// Scalar form: !text menu.start
			if (parser.TryConsume<Scalar>(out var scalar))
			{
				return new TextRefDto { Key = scalar.Value };
			}

			// Mapping form: !text { Key: hud.score, Arguments: [ !var score ] }
			parser.Consume<MappingStart>();

			string? key = null;
			object[]? arguments = null;

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var field = parser.Consume<Scalar>().Value;
				switch (field)
				{
					case "Key":
						key = parser.Consume<Scalar>().Value;
						break;
					case "Arguments":
						var args = rootDeserializer(typeof(List<object>));
						arguments = ((List<object>)args).ToArray();
						break;
					default:
						parser.SkipThisAndNestedEvents();
						break;
				}
			}

			return new TextRefDto { Key = key, Arguments = arguments };
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
