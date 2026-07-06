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
			var position = parser.Current is { } current
				? SourcePosition.At((int)current.Start.Line, (int)current.Start.Column)
				: SourcePosition.Unknown;

			parser.Consume<Scalar>();
			return new GameOverListenerDto { Position = position };
		}

		public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
			throw new NotSupportedException();
	}
}
