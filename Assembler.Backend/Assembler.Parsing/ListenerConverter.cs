using Assembler.Definitions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Assembler.Parsing;

public class ListenersConverter : IYamlTypeConverter
{
	public bool Accepts(Type type) => type == typeof(IReadOnlyList<ListenerDef>);

	public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
	{
		List<ListenerDef> listenerRefs = [];

		parser.Consume<SequenceStart>();

		while (!parser.TryConsume<SequenceEnd>(out _))
		{
			string entityRef = string.Empty;
			string behaviourRef = string.Empty;

			parser.Consume<MappingStart>();

			while (!parser.TryConsume<MappingEnd>(out _))
			{
				var key = parser.Consume<Scalar>();
				var value = parser.Consume<Scalar>();

				switch (key.Value)
				{
					case "entity ref":
						entityRef = value.Value;
						break;
					case "behaviour ref":
						behaviourRef = value.Value;
						break;
				}
			}

			listenerRefs.Add(new ListenerDef { Entity = entityRef, Behaviour = behaviourRef });
		}

		return listenerRefs;
	}

	public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
	{
		if (value is not IReadOnlyList<ListenerDef> listenerRefs)
		{
			throw new ArgumentException($"Expected IReadOnlyList<ListenerDef>, got {value?.GetType()}");
		}

		emitter.Emit(new SequenceStart(null, "!listeners", true, SequenceStyle.Block));

		foreach (var listenerRef in listenerRefs)
		{
			emitter.Emit(new MappingStart(null, null, true, MappingStyle.Block));

			emitter.Emit(new Scalar("entity ref"));
			emitter.Emit(new Scalar(listenerRef.Entity ?? ""));

			emitter.Emit(new Scalar("behaviour ref"));
			emitter.Emit(new Scalar(listenerRef.Behaviour ?? ""));

			emitter.Emit(new MappingEnd());
		}

		emitter.Emit(new SequenceEnd());
	}
}