using System.Collections.Generic;
using System.Linq;
using Assembler.Core;
using Assembler.Parsing.Info;

namespace Assembler.Parsing
{
	/// <summary>
	/// A reference-resolution pass over the transformed <see cref="GameInfo"/>: catches a typo in a
	/// listener's <c>EntityId</c>/<c>BehaviourId</c> at transform time and fails fast with a legible
	/// <see cref="ParsingException"/> naming the bad id and the listener that referenced it. Without this
	/// the same mistake surfaces much later as an opaque <c>KeyNotFoundException</c> from the runtime
	/// behaviour registry (<c>GameBehaviourFactory.ResolveExecutable</c>).
	/// </summary>
	/// <remarks>
	/// Only top-level <see cref="GameInfo.Entities"/> listeners are validated — their behaviours have already
	/// been through <c>TemplateInstantiator.Instantiate</c>, so a <see cref="DirectListenerInfo"/>'s
	/// <c>EntityId</c> is a resolved <see cref="LiteralEntityId"/>. Templates, children (only instantiated at
	/// build time), tagged listeners and the implicit game-over path carry ids/targets that are not knowable
	/// here, so they are deliberately skipped to avoid false positives.
	/// </remarks>
	public static class ReferenceValidator
	{

		public static void Validate(GameInfo gameInfo)
		{
			// Top-level entities keyed to their declared behaviour ids. Post-instantiation, so this already
			// includes template-inherited behaviours. Children are intentionally absent (see below).
			var entityBehaviours = gameInfo.Entities
				.ToDictionary(e => e.Id, e => e.Behaviours.Select(b => b.Id).ToHashSet());

			// Every id that could legitimately be targeted: top-level entities, their composed child ids
			// ({parentId}/{childSuffix}, matching GameEntityFactory), and the implicit game-over entity.
			var knownEntityIds = new HashSet<string> { ReservedIds.GameOverEntityId };

			foreach (var entity in gameInfo.Entities)
			{
				knownEntityIds.Add(entity.Id);
				CollectChildIds(entity.Id, entity.Children, knownEntityIds);
			}

			var placementIds = gameInfo.Placements.Select(p => p.Id).ToArray();

			foreach (var entity in gameInfo.Entities)
			{
				foreach (var behaviour in entity.Behaviours)
				{
					foreach (var listener in behaviour.Listeners.Concat(behaviour.NestedListeners))
					{
						ValidateListener(entity.Id, behaviour.Id, listener, entityBehaviours, knownEntityIds, placementIds);
					}
				}
			}
		}

		private static void CollectChildIds(string parentId,
			IReadOnlyList<ChildEntityInfo> children,
			HashSet<string> knownEntityIds)
		{
			foreach (var child in children)
			{
				var childId = $"{parentId}/{child.IdSuffix}";
				knownEntityIds.Add(childId);
				CollectChildIds(childId, child.Children, knownEntityIds);
			}
		}

		private static void ValidateListener(string entityId,
			string behaviourId,
			ListenerInfo listener,
			IReadOnlyDictionary<string, HashSet<string>> entityBehaviours,
			HashSet<string> knownEntityIds,
			IReadOnlyList<string> placementIds)
		{
			// Only direct, fully-resolved targets are checkable here. Tagged/game-over listeners resolve
			// dynamically; a child/template listener whose EntityId is still Self/Parameter is not a literal.
			if (listener is not DirectListenerInfo { EntityId: LiteralEntityId literal } direct)
			{
				return;
			}

			var targetId = literal.Id;
			var at = direct.SourceSuffix();

			// A runtime-spawned entity's id is only minted at spawn time; the prefix marks it as valid here.
			if (targetId.StartsWith(ReservedIds.SpawnedIdPrefix))
			{
				return;
			}

			// The implicit game-over entity exposes exactly one behaviour: 'end'.
			if (targetId == ReservedIds.GameOverEntityId)
			{
				if (direct.BehaviourId != ReservedIds.GameOverBehaviourId)
				{
					throw new ParsingException(
						$"Behaviour '{behaviourId}' on entity '{entityId}'{at} targets '{ReservedIds.GameOverEntityId}' but behaviour " +
						$"'{direct.BehaviourId}' — the only game-over behaviour is '{ReservedIds.GameOverBehaviourId}'.");
				}

				return;
			}

			// A placement stamps out instances '{placementId}_{i}'. Only the entity id is checkable at
			// transform time — behaviour ids on placement instances come from the (build-time) template.
			if (placementIds.Any(placementId => IsPlacementInstance(targetId, placementId)))
			{
				return;
			}

			if (!knownEntityIds.Contains(targetId))
			{
				throw new ParsingException(
					$"Behaviour '{behaviourId}' on entity '{entityId}'{at} targets unknown entity '{targetId}'.");
			}

			// A known child id (present in the set but not the top-level behaviour map) can't have its
			// behaviour ids checked — a child may inherit behaviours from a template only bound at build time.
			if (!entityBehaviours.TryGetValue(targetId, out var declaredBehaviours))
			{
				return;
			}

			if (!declaredBehaviours.Contains(direct.BehaviourId))
			{
				throw new ParsingException(
					$"Behaviour '{behaviourId}' on entity '{entityId}'{at} targets behaviour '{direct.BehaviourId}' which " +
					$"does not exist on entity '{targetId}' (declared behaviours: {string.Join(", ", declaredBehaviours)}).");
			}
		}

		// True when id is '{placementId}_{n}' for a non-negative integer n.
		private static bool IsPlacementInstance(string id, string placementId)
		{
			var prefix = $"{placementId}_";
			return id.StartsWith(prefix)
				&& id.Length > prefix.Length
				&& id.Skip(prefix.Length).All(char.IsDigit);
		}
	}
}
