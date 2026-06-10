using System;
using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	// Reads a `!record { Type: Item, count: 3 }` mapping into a RecordLiteralDto. A record literal has
	// dynamic keys (the field names), so unlike VecDto it can't use fixed properties: the reserved `Type`
	// key supplies the schema name and every other key is collected into the Fields map. Values go through
	// rootDeserializer so nested tagged nodes (e.g. !var) inside a field still parse.
	internal class RecordTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(RecordLiteralDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			// A `!record [ … ]` list — the tag mapping routes both the mapping and sequence forms here,
			// so (like VecTypeConverter) handle the sequence by reading each bare-mapping element.
			if (parser.Accept<SequenceStart>(out _))
			{
				parser.Consume<SequenceStart>();
				var list = new List<RecordLiteralDto>();

				while (!parser.TryConsume<SequenceEnd>(out _))
				{
					list.Add((RecordLiteralDto)ReadYaml(parser, type, rootDeserializer));
				}

				return list;
			}

			parser.Consume<MappingStart>();

			string? recordType = null;
			var fields = new Dictionary<string, object>();

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				var value = rootDeserializer(typeof(object));

				if (key == "Type")
				{
					recordType = value as string;
				}
				else
				{
					fields[key] = value!;
				}
			}

			return new RecordLiteralDto { Type = recordType, Fields = fields };
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
