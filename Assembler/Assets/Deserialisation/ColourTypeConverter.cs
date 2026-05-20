using System;
using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	internal class ColourTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(ColourDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			if (parser.TryConsume<Scalar>(out var scalar))
			{
				return new ColourDto { Raw = scalar.Value };
			}

			if (parser.Accept<SequenceStart>(out _))
			{
				parser.Consume<SequenceStart>();
				var list = new List<ColourDto>();

				while (!parser.TryConsume<SequenceEnd>(out _))
				{
					list.Add((ColourDto)ReadYaml(parser, type, rootDeserializer));
				}

				return list;
			}

			parser.Consume<MappingStart>();

			object? r = null;
			object? g = null;
			object? b = null;
			object? a = null;

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				var value = rootDeserializer(typeof(object));

				switch (key)
				{
					case "R":
						r = value;
						break;
					case "G":
						g = value;
						break;
					case "B":
						b = value;
						break;
					case "A":
						a = value;
						break;
				}
			}

			return new ColourDto
			{
				R = r,
				G = g,
				B = b,
				A = a
			};
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
