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

			string? id = null;
			string? property = null;

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				switch (key)
				{
					case "Id":
						id = parser.Consume<Scalar>().Value;
						break;
					case "Property":
						property = parser.Consume<Scalar>().Value;
						break;
					default:
						parser.SkipThisAndNestedEvents();
						break;
				}
			}

			if (string.IsNullOrWhiteSpace(id))
			{
				throw new YamlException("!entity requires a non-empty 'Id' key (the entity to read).");
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
