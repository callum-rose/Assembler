using System;
using System.Collections.Generic;
using Assembler.Parsing.Controls;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;

namespace Assembler.Input
{
	/// <summary>
	/// Hard-fail gate, run from <c>Builder.Build</c> alongside the existing game-over check: scans the
	/// template-expanded <see cref="GameInfo"/> for <see cref="InputActionTriggerInfo"/> behaviours, collects the
	/// action names they reference, and throws if any used action is undeclared or has no binding for the active
	/// platform group. This guarantees a game never ships with silently-dead input.
	/// </summary>
	public static class ControlsValidator
	{
		public static void Validate(GameInfo gameInfo, ControlsInfo controls, string activeGroup)
		{
			var usedActions = new HashSet<string>();

			foreach (var entity in gameInfo.Entities)
			{
				CollectFromBehaviours(entity.Behaviours, gameInfo, usedActions);
				CollectFromChildren(entity.Children, gameInfo, usedActions);
			}

			foreach (var template in gameInfo.Templates)
			{
				CollectFromBehaviours(template.Behaviours, gameInfo, usedActions);
				CollectFromChildren(template.Children, gameInfo, usedActions);
			}

			controls.Bindings.TryGetValue(activeGroup, out var groupBindings);

			foreach (var actionName in usedActions)
			{
				if (!controls.Actions.ContainsKey(actionName))
				{
					throw new InvalidOperationException(
						$"Input action '{actionName}' is used by a behaviour but is not declared in " +
						"the descriptor's Controls.Actions section.");
				}

				var hasBinding = groupBindings != null
								 && groupBindings.TryGetValue(actionName, out var bindings)
								 && bindings.Count > 0;

				if (!hasBinding)
				{
					throw new InvalidOperationException(
						$"Input action '{actionName}' has no binding for platform '{activeGroup}'. " +
						"Add it under Controls.Bindings for that platform (or a platform it falls back to).");
				}
			}
		}

		private static void CollectFromChildren(IReadOnlyList<ChildEntityInfo> children,
			GameInfo gameInfo,
			HashSet<string> usedActions)
		{
			foreach (var child in children)
			{
				CollectFromBehaviours(child.Behaviours, gameInfo, usedActions);
				CollectFromChildren(child.Children, gameInfo, usedActions);
			}
		}

		private static void CollectFromBehaviours(IReadOnlyList<BehaviourInfo> behaviours,
			GameInfo gameInfo,
			HashSet<string> usedActions)
		{
			foreach (var behaviour in behaviours)
			{
				if (behaviour is InputActionTriggerInfo trigger
					&& TryResolveActionName(trigger.Action, gameInfo, out var name))
				{
					usedActions.Add(name);
				}
			}
		}

		// Action names are statically known when authored as a literal or a variable reference (the common
		// cases, including template parameters that were substituted during transformation). Names produced by
		// expressions or other dynamic sources can't be validated ahead of time, so they're left to the runtime
		// FindAction lookup in the builder entry.
		private static bool TryResolveActionName(ValueSource<string> source, GameInfo gameInfo, out string name)
		{
			switch (source)
			{
				case ConstantSource<string> constant:
					name = constant.Value;
					return true;
				case ValueReferenceSource<string> reference:
					foreach (var variable in gameInfo.Variables)
					{
						if (variable.Id == reference.VariableId && variable.Value is StringValue stringValue)
						{
							name = stringValue.Value;
							return true;
						}
					}

					break;
			}

			name = string.Empty;
			return false;
		}
	}
}
