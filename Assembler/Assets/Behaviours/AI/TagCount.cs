using Assembler.Behaviours.Triggers;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.AI
{
	/// <summary>Counts the live entities carrying a tag and re-emits to its listeners with that count as an output.</summary>
	/// <remarks>
	/// A global census computed on demand: wire it as a listener to whatever should trigger a recount (an
	/// enemy-death trigger, a periodic timer, a spawn) and on each Execute it counts every live entity carrying
	/// <c>Tag</c> across the whole world — no host position, radius, or allocation — then forwards the trigger to
	/// its own <c>Listeners</c> with the tally added as the <c>count</c> output. It preserves any upstream outputs
	/// on the context. This is the primitive for "all enemies destroyed" / "no targets remain" win-lose checks:
	/// point a <c>condition gate</c> on <c>!output count</c> (e.g. <c>count &lt;= 0</c>) at it, or pipe <c>count</c>
	/// into an <c>int variable setter</c> to persist it. The count reflects live entities only, and the
	/// <c>destroy</c> behaviour evicts an entity from the index the moment it runs, so a recount wired downstream
	/// of a kill already excludes the entity just destroyed — no off-by-one, no drift the way a hand-maintained
	/// counter has.
	/// Properties:
	///   Tag: Entity tag to count.
	/// Outputs:
	///   count [int]: The number of live entities carrying Tag at the moment of counting.
	/// </remarks>
	public sealed class TagCount : Trigger<TagCountData>, INeedsEntityQuery, IAmExecutable
	{
		public EntityQueryService Query { get; set; } = null!;

		public void Execute(TriggerContext ctx)
		{
			var count = Query.CountByTag(Data.Tag.Get(ctx));
			NotifyListeners(ctx.With("count", count));
		}
	}
}
