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
		IReadOnlyList<ConcreteEntityInfo> Entities)
	{
		/// <summary>
		/// The context built during <see cref="Transformer.Transform"/>. Cached so runtime
		/// callers (e.g. spawners) can re-enter <see cref="TemplateInstantiator.Instantiate"/>
		/// with the same expressions/type-registry/values they were parsed against.
		/// Populated by <see cref="Transformer.Transform"/>; not part of the positional ctor.
		/// </summary>
		public TransformContext ParseContext { get; init; } = null!;
	}
}
