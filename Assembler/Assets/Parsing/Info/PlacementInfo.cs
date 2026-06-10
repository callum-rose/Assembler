using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info
{
	/// <summary>
	/// A resolved Placements entry: a reusable template (<see cref="TemplateId"/>) stamped out at the
	/// positions <see cref="Positions"/> yields (a literal list or an <c>!expr</c> returning a
	/// <c>List&lt;Vector3&gt;</c>). <see cref="Positions"/> is resolved at build time — it needs the
	/// runtime variable/expression registries — by <see cref="Assembler.Building.GameEntityFactory.ExpandPlacement"/>,
	/// which instantiates one independent entity per position via the normal template-instantiation path.
	/// <see cref="Rotation"/> is a single euler shared by every instance; <see cref="Parameters"/> are
	/// forwarded to the template's parameter slots (the same value for every instance).
	/// </summary>
	public sealed record PlacementInfo(
		string Id,
		string TemplateId,
		ValueSource<List<Vector3>> Positions,
		ValueSource<Vector3> Rotation,
		IReadOnlyDictionary<string, AssemblerValue> Parameters,
		IReadOnlyList<string> Tags);
}
