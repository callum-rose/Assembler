using System;
using Parsing.Phase1.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Parsing.Phase1
{
	internal class VarTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(VarRefDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			var scalar = parser.Consume<Scalar>();
			return new VarRefDto
			{
				Id = scalar.Value
			};
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}