using System;
using Assembler.Deserialisation.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Deserialisation
{
	internal class GameOverListenerTypeConverter : IYamlTypeConverter
	{
		public bool Accepts(Type type) => type == typeof(GameOverListenerDto);

		public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
		{
			parser.Consume<Scalar>();
			return new GameOverListenerDto();
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
