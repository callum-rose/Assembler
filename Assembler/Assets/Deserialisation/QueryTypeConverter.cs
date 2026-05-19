using System;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	internal class QueryTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(QueryRefDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			parser.Consume<MappingStart>();

			string? tag = null;

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				switch (key)
				{
					case "Tag":
						tag = parser.Consume<Scalar>().Value;
						break;
					default:
						parser.SkipThisAndNestedEvents();
						break;
				}
			}

			return new QueryRefDto { Tag = tag };
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
