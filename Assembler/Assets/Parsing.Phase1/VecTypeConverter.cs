using System;
using Parsing.Phase1.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Parsing.Phase1
{
	internal class VecTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(VecDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			var vec = new VecDto();
			parser.Consume<MappingStart>();
			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>().Value;
				var value = rootDeserializer(typeof(object));
				switch (key)
				{
					case "X": vec.X = value; break;
					case "Y": vec.Y = value; break;
					case "Z": vec.Z = value; break;
				}
			}
			return vec;
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}