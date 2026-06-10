using System;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	internal class EntityQueryTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(EntityQueryRefDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			parser.Consume<MappingStart>();

			string? kind = null;
			string? entityTag = null;
			object? origin = null;
			object? maxRange = null;

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				switch (key)
				{
					case "Kind":
						kind = parser.Consume<Scalar>().Value;
						break;
					case "EntityTag":
						entityTag = parser.Consume<Scalar>().Value;
						break;
					// Origin/MaxRange may themselves be tagged values (e.g. !entity, !var), so defer to the root
					// deserializer rather than consuming a bare scalar.
					case "Origin":
						origin = rootDeserializer(typeof(object));
						break;
					case "MaxRange":
						maxRange = rootDeserializer(typeof(object));
						break;
					default:
						parser.SkipThisAndNestedEvents();
						break;
				}
			}

			if (string.IsNullOrWhiteSpace(kind))
			{
				throw new YamlException("!query requires a non-empty 'Kind' key (e.g. NearestId, NearestPosition).");
			}

			if (string.IsNullOrWhiteSpace(entityTag))
			{
				throw new YamlException("!query requires a non-empty 'EntityTag' key (the entity tag to search for).");
			}

			return new EntityQueryRefDto
			{
				Kind = kind,
				EntityTag = entityTag,
				Origin = origin,
				MaxRange = maxRange
			};
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
