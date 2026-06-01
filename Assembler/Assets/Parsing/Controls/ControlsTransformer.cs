using System.Collections.Generic;
using System.Linq;
using Assembler.Deserialisation.Dtos;
using Assembler.Extensions;
using Assembler.Parsing.Info.Behaviours;

namespace Assembler.Parsing.Controls
{
	/// <summary>
	/// Converts the raw <see cref="ControlsDto"/> (deserialised from the descriptor's <c>Controls</c> section)
	/// into the validated <see cref="ControlsInfo"/> consumed by <see cref="InputActionBuilder"/> and
	/// <see cref="ControlsValidator"/>. Mirrors the DTO → Info step the <c>Transformer</c> performs for the
	/// rest of the descriptor.
	/// </summary>
	public static class ControlsTransformer
	{
		public static ControlsInfo Transform(ControlsDto? dto)
		{
			if (dto is null)
			{
				return ControlsInfo.Empty;
			}

			var actions = dto.Actions
				.EmptyIfNull()
				.ToDictionary(t => t.Key, t => TransformAction(t.Key, t.Value));

			var bindings = dto.Bindings
				.EmptyIfNull()
				.ToDictionary(
					p => p.Key,
					p => (IReadOnlyDictionary<string, IReadOnlyList<BindingInfo>>)p.Value
						.EmptyIfNull()
						.ToDictionary(
							a => a.Key,
							a => (IReadOnlyList<BindingInfo>)a.Value
								.EmptyIfNull()
								.Select(b => TransformBinding(p.Key, a.Key, b))
								.ToList()));

			return new ControlsInfo(actions, bindings);
		}

		private static ActionInfo TransformAction(string name, ActionDto dto)
		{
			var kind = (dto.Type ?? "button").ToLowerInvariant() switch
			{
				"button" => ActionKind.Button,
				"value" => ActionKind.Value,
				var other => throw new ParsingException(
					$"Action '{name}' has unknown Type '{other}'. Expected 'button' or 'value'.")
			};

			var phase = (dto.Phase ?? "hold").ToLowerInvariant() switch
			{
				"hold" => ButtonPhase.Hold,
				"down" => ButtonPhase.Down,
				"up" => ButtonPhase.Up,
				var other => throw new ParsingException(
					$"Action '{name}' has unknown Phase '{other}'. Expected 'hold', 'down' or 'up'.")
			};

			// Value actions are built as Vector2 and read via ReadValue<Vector2> in the trigger, so reject any
			// other ValueType up front rather than silently coercing a 1D/scalar declaration into a vector.
			if (kind == ActionKind.Value && (dto.ValueType ?? "vector2").ToLowerInvariant() != "vector2")
			{
				throw new ParsingException(
					$"Action '{name}' has unsupported ValueType '{dto.ValueType}'. Only 'vector2' is currently supported.");
			}

			return new ActionInfo(name, kind, phase, dto.ValueType);
		}

		private static BindingInfo TransformBinding(string platform, string actionName, BindingDto dto)
		{
			if (dto.Composite != null)
			{
				return BindingInfo.CompositeOf(dto.Composite, dto.Parts ?? new Dictionary<string, string>());
			}

			if (!string.IsNullOrEmpty(dto.Path))
			{
				return BindingInfo.Simple(dto.Path!);
			}

			throw new ParsingException(
				$"Binding for action '{actionName}' on platform '{platform}' is neither a control path nor a composite.");
		}
	}
}
