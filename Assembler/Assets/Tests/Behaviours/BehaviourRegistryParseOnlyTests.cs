using System;
using System.Linq;
using Assembler.Building;
using NUnit.Framework;
using BehaviourRegistry = Assembler.Parsing.BehaviourRegistry;

namespace Tests.Behaviours
{
	/// <summary>
	/// Drift guard for the parse/build split: every behaviour name the parser catalogues
	/// (<see cref="BehaviourRegistry.All"/>) must either have a runtime builder in
	/// <see cref="GameBehaviourFactory"/> or be explicitly listed in
	/// <see cref="BehaviourRegistry.ParseOnly"/>. Without this, a behaviour added to the catalogue without a
	/// builder parses fine and then dies far downstream at instantiate with a CLR type name — the exact
	/// silent-until-downstream failure issue #237 set out to kill.
	/// </summary>
	public class BehaviourRegistryParseOnlyTests
	{
		// The concrete BehaviourInfo type each catalogue entry produces. The factory delegates are bound to
		// concrete `XxxInfo.Create` methods, so the bound method's return type is the closed concrete Info
		// type (e.g. WhenAllInfo, VariableSetterInfo<Vector3>) even though the delegate is declared to return
		// the BehaviourInfo base — no factory invocation needed.
		private static Type InfoType(string name) => BehaviourRegistry.All[name].Method.ReturnType;

		[Test]
		public void EveryRunnableBehaviourHasABuilder()
		{
			var builderInfoTypes = GameBehaviourFactory.MonoBehaviourByInfo.Keys.ToHashSet();

			var missing = BehaviourRegistry.All.Keys
				.Where(name => !BehaviourRegistry.ParseOnly.Contains(name))
				.Where(name => !builderInfoTypes.Contains(InfoType(name)))
				.ToArray();

			Assert.IsEmpty(missing,
				"These behaviours are in the parse catalogue but have no GameBehaviourFactory builder and are " +
				"not listed in BehaviourRegistry.ParseOnly. Add a builder (and a Data/MonoBehaviour) to make " +
				$"them runnable, or add them to ParseOnly: {string.Join(", ", missing)}");
		}

		[Test]
		public void ParseOnlyBehavioursAreRecognisedAndUnbuilt()
		{
			var builderInfoTypes = GameBehaviourFactory.MonoBehaviourByInfo.Keys.ToHashSet();

			foreach (var name in BehaviourRegistry.ParseOnly)
			{
				Assert.IsTrue(BehaviourRegistry.All.ContainsKey(name),
					$"ParseOnly lists '{name}' but it is not in the parse catalogue (BehaviourRegistry.All).");

				Assert.IsFalse(builderInfoTypes.Contains(InfoType(name)),
					$"'{name}' is marked ParseOnly but now has a GameBehaviourFactory builder — it is runnable, " +
					"so remove it from BehaviourRegistry.ParseOnly.");
			}
		}
	}
}
