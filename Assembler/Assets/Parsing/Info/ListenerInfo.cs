using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public abstract record ListenerInfo
	{
		public IReadOnlyDictionary<string, string> OutputMapping { get; init; } = new Dictionary<string, string>();
	}

	/// <summary>Targets a specific behaviour by entity id + behaviour id. <see cref="EntityId"/> is a
	/// <see cref="ParameterisableEntityId"/>, so an id authored as <c>!parameter &lt;name&gt;</c> stays
	/// pending until template instantiation resolves it (see <c>TemplateInstantiator.SubstituteListeners</c>).
	/// By wiring time the id is always a literal, so <see cref="BehaviourDescriptor"/> reads it directly.</summary>
	public sealed record DirectListenerInfo(ParameterisableEntityId EntityId, string BehaviourId) : ListenerInfo
	{
		public BehaviourDescriptor BehaviourDescriptor => new(EntityId.Id, BehaviourId);
	}

	public sealed record EntityTaggedListenerInfo(
		ValueSource<string> EntityTag,
		string? BehaviourId) : ListenerInfo;

	public sealed record BehaviourTaggedListenerInfo(
		ValueSource<string> BehaviourTag) : ListenerInfo;

	/// <summary>Targets the implicit end-game behaviour. Produced by the <c>!gameover</c> listener tag.</summary>
	public sealed record GameOverListenerInfo : ListenerInfo;
}
