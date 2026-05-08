using Assembler.Parsing.Phase1.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Parsing.Phase1;

internal class RefTypeConverter : IYamlTypeConverter
{
	public bool Accepts(Type type) => type == typeof(RefDto);

	public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
	{
		var scalar = parser.Consume<Scalar>();
		return new RefDto { Id = scalar.Value };
	}

	public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
		throw new NotSupportedException();
}