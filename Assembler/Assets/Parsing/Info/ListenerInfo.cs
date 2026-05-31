using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public abstract record ListenerInfo
	{
		public IReadOnlyDictionary<string, string> OutputMapping { get; init; } = new Dictionary<string, string>();

		/// <summary>
		/// Branch channel this listener belongs to. Only consulted by routing behaviours (e.g. <c>branch</c>):
		/// the behaviour fires the listeners whose <see cref="When"/> matches its evaluated condition.
		/// Ignored by behaviours that notify all listeners unconditionally. Defaults to <c>true</c>.
		/// </summary>
		public bool When { get; init; } = true;
	}

	public sealed record DirectListenerInfo(BehaviourDescriptor BehaviourDescriptor) : ListenerInfo;

	public sealed record EntityTaggedListenerInfo(
		ValueSource<string> EntityTag,
		string? BehaviourId) : ListenerInfo;

	public sealed record BehaviourTaggedListenerInfo(
		ValueSource<string> BehaviourTag) : ListenerInfo;

	/// <summary>Targets the implicit end-game behaviour. Produced by the <c>!gameover</c> listener tag.</summary>
	public sealed record GameOverListenerInfo : ListenerInfo;
}
