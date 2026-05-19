using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ForEachTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<IReadOnlyList<string>> Entities)
		: BehaviourInfo(Id, Listeners)
	{
		public static ForEachTriggerInfo Create(
			string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p)
		{
			var raw = props?.GetValueOrDefault("Entities");

			ValueSource<IReadOnlyList<string>> entities = raw switch
			{
				QueryRefDto q => new TagQuerySource(q.Tag ?? string.Empty),
				_             => Transformer.CreateValueSource<IReadOnlyList<string>>(v, raw, parameters: p)
			};

			return new ForEachTriggerInfo(id, listeners, entities);
		}

		public override BehaviourInfo SubstituteParameters(
			IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ForEachTriggerInfo(Id, substitutedListeners,
				Entities.SubstituteParameters(parameters, allValues));
	}
}
