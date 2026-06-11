using System;
using System.Linq;
using Assembler.Building;
using NUnit.Framework;
using BehaviourRegistry = Assembler.Parsing.BehaviourRegistry;

namespace Tests.Behaviours
{
	/// <summary>
	/// Drift guard for the parse/build split: every behaviour name the parser catalogues
	/// (<see cref="BehaviourRegistry.All"/>) must have a runtime builder in <see cref="GameBehaviourFactory"/>.
	/// There are no parse-only behaviours — a catalogued behaviour with no builder parses fine and then dies
	/// far downstream at instantiate with a CLR type name, which this catches up front.
	/// </summary>
	public class BehaviourRegistryBuilderCoverageTests
	{
		[Test]
		public void EveryCataloguedBehaviourHasABuilder()
		{
			var builderInfoTypes = GameBehaviourFactory.MonoBehaviourByInfo.Keys.ToHashSet();

			// The factory delegates are bound to concrete `XxxInfo.Create` methods, so the bound method's
			// return type is the closed concrete Info type (e.g. VariableSetterInfo<Vector3>) even though the
			// delegate is declared to return the BehaviourInfo base — no factory invocation needed.
			var missing = BehaviourRegistry.All
				.Where(kv => !builderInfoTypes.Contains(kv.Value.Method.ReturnType))
				.Select(kv => kv.Key)
				.ToArray();

			Assert.IsEmpty(missing,
				"These behaviours are in the parse catalogue but have no GameBehaviourFactory builder, so they " +
				"would parse and then fail at instantiate. Add a builder (and a Data/MonoBehaviour), or remove " +
				$"them from BehaviourRegistry.All: {string.Join(", ", missing)}");
		}
	}
}
