using System;
using Assembler.Parsing.Phase1.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Parsing.Phase1
{
	internal class VecTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(VecDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			parser.Consume<MappingStart>();

			object? x = null;
			object? y = null;
			object? z = null;

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				var value = rootDeserializer(typeof(object));

				switch (key)
				{
					case "X":
						x = value;
						break;
					case "Y":
						y = value;
						break;
					case "Z":
						z = value;
						break;
				}
			}

			return new VecDto
			{
				X = x,
				Y = y,
				Z = z
			};
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}