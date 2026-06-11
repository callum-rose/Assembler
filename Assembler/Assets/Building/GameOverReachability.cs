using System.Collections.Generic;
using System.Linq;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;

namespace Assembler.Building
{
	/// <summary>
	/// Build-time guard confirming a descriptor can actually reach a game-over. The guard is satisfied when
	/// some <em>instantiated</em> entity declares a <c>!gameover</c> listener — whether at the top level of a
	/// behaviour or in a nested site such as a state machine's OnEnter/OnExit hook (see
	/// <see cref="BehaviourInfo.NestedListeners"/>).
	/// <para>
	/// Concrete entities and Placements are always instantiated; a template counts only when it is reachable
	/// from one of those — via a child template reference (<see cref="ChildEntityInfo.TemplateRefId"/>) or a
	/// spawner — transitively. A <c>!gameover</c> sitting only inside a never-instantiated template therefore
	/// does <em>not</em> satisfy the guard.
	/// </para>
	/// </summary>
	public static class GameOverReachability
	{
		public static bool HasReachableGameOver(GameInfo gameInfo)
		{
			var templatesById = gameInfo.Templates.ToDictionary(t => t.Id, t => t);
			var reachableTemplates = ReachableTemplates(gameInfo, templatesById);

			// Concrete entities always instantiate; reachable templates instantiate via placements/spawners/child refs.
			return gameInfo.Entities.Cast<EntityInfo>()
				.Concat(reachableTemplates)
				.Any(DeclaresGameOver);
		}

		private static IEnumerable<EntityInfo> ReachableTemplates(
			GameInfo gameInfo,
			IReadOnlyDictionary<string, EntityInfo> templatesById)
		{
			var visited = new HashSet<string>();
			var reachable = new List<EntityInfo>();

			// Seeds: template ids referenced from always-instantiated sites (concrete entities and placements).
			var pending = new Queue<string>(
				gameInfo.Placements.Select(p => p.TemplateId)
					.Concat(gameInfo.Entities.SelectMany(TemplateReferences)));

			while (pending.Count > 0)
			{
				var id = pending.Dequeue();
				if (!visited.Add(id) || !templatesById.TryGetValue(id, out var template))
				{
					continue;
				}

				reachable.Add(template);

				foreach (var next in TemplateReferences(template))
				{
					pending.Enqueue(next);
				}
			}

			return reachable;
		}

		// Template ids referenced anywhere within an entity's subtree: child template refs and spawner targets.
		// A spawner's TemplateId is a ValueSource, so only a constant id can be resolved statically; dynamic
		// (expression/reference) targets are conservatively ignored, matching the guard's bias toward demanding
		// an unambiguous game-over path over silently accepting one that may never be reached.
		private static IEnumerable<string> TemplateReferences(EntityInfo entity) =>
			BehaviourTemplateReferences(entity.Behaviours)
				.Concat(entity.Children.SelectMany(TemplateReferences));

		private static IEnumerable<string> TemplateReferences(ChildEntityInfo child) =>
			(child.TemplateRefId is { } refId ? new[] { refId } : Enumerable.Empty<string>())
				.Concat(BehaviourTemplateReferences(child.Behaviours))
				.Concat(child.Children.SelectMany(TemplateReferences));

		private static IEnumerable<string> BehaviourTemplateReferences(IEnumerable<BehaviourInfo> behaviours) =>
			behaviours.OfType<SpawnerInfo>().SelectMany(SpawnerTemplateReferences);

		private static IEnumerable<string> SpawnerTemplateReferences(SpawnerInfo spawner)
		{
			var weighted = spawner.Templates.Select(t => t.TemplateId);
			return spawner.TemplateId is ConstantSource<string> { Value: { Length: > 0 } id }
				? weighted.Append(id)
				: weighted;
		}

		private static bool DeclaresGameOver(EntityInfo entity) =>
			BehavioursDeclareGameOver(entity.Behaviours) || entity.Children.Any(DeclaresGameOver);

		private static bool DeclaresGameOver(ChildEntityInfo child) =>
			BehavioursDeclareGameOver(child.Behaviours) || child.Children.Any(DeclaresGameOver);

		private static bool BehavioursDeclareGameOver(IEnumerable<BehaviourInfo> behaviours) =>
			behaviours.Any(b => b.Listeners.Concat(b.NestedListeners).Any(l => l is GameOverListenerInfo));
	}
}
