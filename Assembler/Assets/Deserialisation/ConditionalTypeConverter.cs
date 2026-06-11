using System;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	internal class ConditionalTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(ConditionalRefDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			parser.Consume<MappingStart>();

			object? condition = null;
			object? then = null;
			object? @else = null;

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				switch (key)
				{
					// Every branch may itself be a tagged value (e.g. !var, !expr, !vec), so defer to the root
					// deserializer rather than consuming a bare scalar.
					case "Condition":
						condition = rootDeserializer(typeof(object));
						break;
					case "Then":
						then = rootDeserializer(typeof(object));
						break;
					case "Else":
						@else = rootDeserializer(typeof(object));
						break;
					default:
						parser.SkipThisAndNestedEvents();
						break;
				}
			}

			if (condition is null)
			{
				throw new YamlException("!if requires a 'Condition' key (a boolean value).");
			}

			if (then is null)
			{
				throw new YamlException("!if requires a 'Then' key (the value used when the condition is true).");
			}

			if (@else is null)
			{
				throw new YamlException("!if requires an 'Else' key (the value used when the condition is false).");
			}

			return new ConditionalRefDto
			{
				Condition = condition,
				Then = then,
				Else = @else
			};
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
