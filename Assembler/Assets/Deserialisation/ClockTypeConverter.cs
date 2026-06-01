using System;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	internal class ClockTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(ClockRefDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			var scalar = parser.Consume<Scalar>();
			return new ClockRefDto
			{
				Property = scalar.Value
			};
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
