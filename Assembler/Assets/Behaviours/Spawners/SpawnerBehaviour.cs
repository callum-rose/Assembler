using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Libraries;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Spawners
{
	public interface INeedsSpawner
	{
		IEntitySpawner Spawner { get; set; }
	}

	/// <summary>Spawns an instance of a template at a position when Executed.</summary>
	/// <remarks>
	/// Properties:
	///   TemplateId: Id of the template to instantiate. Used as a fallback when Templates is empty.
	///   Templates: Optional list of template ids (or { Template, Weight } maps); one is chosen per spawn. Takes precedence over TemplateId when non-empty.
	///   Selection: How Templates is sampled: 'random' (weighted, the default) or 'sequential' (round-robin in list order; weights ignored).
	///   Position: World-space position for the spawned entity.
	///   Rotation: Euler rotation in degrees for the spawned entity.
	///   Parameters: Optional name→value overrides forwarded to the template's parameter slots.
	/// </remarks>
	public class SpawnerBehaviour : GameBehaviour<SpawnerData>, INeedsSpawner, IAmExecutable
	{
		public IEntitySpawner Spawner { get; set; }

		private int _sequentialIndex;

		public void Execute(TriggerContext ctx)
		{
			Spawner.Spawn(PickTemplateId(ctx),
				Data.Position.Get(ctx),
				Data.Rotation.Get(ctx),
				Data.Parameters.ToDictionary(kv => kv.Key, kv => kv.Value.Get(ctx)));

			NotifyListeners(ctx);
		}

		private string PickTemplateId(TriggerContext ctx)
		{
			var templates = Data.Templates;
			if (templates.Count == 0)
			{
				return Data.HasTemplateId
					? Data.TemplateId.Get(ctx)
					: throw new InvalidOperationException(
						$"Spawner '{Data.Id}' has neither a non-empty Templates list nor a TemplateId.");
			}

			var selection = Data.Selection.ValueOr("random").Trim().ToLowerInvariant();
			return selection == "sequential" ? PickSequential(templates) : PickWeightedRandom(templates, ctx);
		}

		private string PickSequential(IReadOnlyList<SpawnTemplate> templates)
		{
			var choice = templates[_sequentialIndex];
			_sequentialIndex = (_sequentialIndex + 1) % templates.Count;
			return choice.TemplateId;
		}

		private static string PickWeightedRandom(IReadOnlyList<SpawnTemplate> templates, TriggerContext ctx)
		{
			var weights = templates.Select(t => t.Weight.Get(ctx)).ToList();
			return templates[RandomMath.WeightedPickIndex(weights)].TemplateId;
		}
	}
}
