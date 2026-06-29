using System;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	internal class EntityTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(EntityRefDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			parser.Consume<MappingStart>();

			object? id = null;
			string? property = null;

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				switch (key)
				{
					// Id may be a tagged value (e.g. !parameter self_id), so defer to the root deserializer
					// rather than consuming a bare scalar — a plain id deserialises to a string, a !parameter
					// to a ParamRefDto.
					case "Id":
						id = rootDeserializer(typeof(object));
						break;
					case "Property":
						property = parser.Consume<Scalar>().Value;
						break;
					default:
						parser.SkipThisAndNestedEvents();
						break;
				}
			}

			// Id is optional: omit it entirely to reference the enclosing entity ("self"). An empty/whitespace
			// id, however, is an authoring mistake rather than the self shorthand, so it still errors.
			if (id is string s && string.IsNullOrWhiteSpace(s))
			{
				throw new YamlException(
					"!entity 'Id' must be a non-empty entity id when present; omit it entirely to reference the enclosing entity.");
			}

			if (string.IsNullOrWhiteSpace(property))
			{
				throw new YamlException("!entity requires a non-empty 'Property' key (e.g. Position, Rotation, Scale).");
			}

			return new EntityRefDto
			{
				Id = id,
				Property = property
			};
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
