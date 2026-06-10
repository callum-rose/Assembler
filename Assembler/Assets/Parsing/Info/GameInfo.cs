using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public record GameInfo(
		AboutInfo About,
		WorldInfo World,
		PhysicsInfo Physics,
		IReadOnlyList<AssetInfo> Assets,
		LocalisationInfo Localisation,
		IReadOnlyList<ValueInfo> Variables,
		IReadOnlyList<ExpressionInfo> Expressions,
		IReadOnlyList<EntityInfo> Templates,
		IReadOnlyList<ConcreteEntityInfo> Entities,
		IReadOnlyList<PlacementInfo> Placements)
	{
		/// <summary>
		/// The context built during <see cref="Transformer.Transform"/>. Cached so runtime
		/// callers (e.g. spawners) can re-enter <see cref="TemplateInstantiator.Instantiate"/>
		/// with the same expressions/type-registry/values they were parsed against.
		/// Populated by <see cref="Transformer.Transform"/>; not part of the positional ctor.
		/// </summary>
		public TransformContext ParseContext { get; init; } = null!;

		/// <summary>
		/// The grid-navigation configuration from the descriptor's <c>Navigation:</c> section, or
		/// <see cref="NavigationInfo.Default"/> when absent. Not part of the positional ctor; populated by
		/// <see cref="Transformer.Transform"/>.
		/// </summary>
		public NavigationInfo Navigation { get; init; } = NavigationInfo.Default;
	}
}
