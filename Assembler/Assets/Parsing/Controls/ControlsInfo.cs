using System.Collections.Generic;

namespace Assembler.Parsing.Controls
{
	/// <summary>
	/// The parsed, validated <c>Controls</c> section: the named abstract <see cref="ActionInfo"/>s a game uses,
	/// plus the per-platform <see cref="BindingInfo"/>s that map physical inputs onto them. Carried through the
	/// build context (never onto <c>GameInfo</c>) so the rest of the pipeline stays free of input concerns.
	/// </summary>
	/// <param name="Actions">Declared actions keyed by action id.</param>
	/// <param name="Bindings">
	/// Platform key (<c>desktop</c>/<c>gamepad</c>/<c>mobile</c>/<c>console</c>) → action id → its bindings.
	/// </param>
	/// <param name="OnScreen">On-screen touch widgets, rendered only when the active platform is Mobile.</param>
	public sealed record ControlsInfo(
		IReadOnlyDictionary<string, ActionInfo> Actions,
		IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<BindingInfo>>> Bindings,
		IReadOnlyList<OnScreenControlInfo> OnScreen)
	{
		public readonly static ControlsInfo Empty = new(
			new Dictionary<string, ActionInfo>(),
			new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<BindingInfo>>>(),
			new List<OnScreenControlInfo>());
	}
}
