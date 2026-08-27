using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>One primitive piece of a <c>model</c>. <see cref="Position"/>, <see cref="Rotation"/>,
	/// <see cref="Size"/> and <see cref="Colour"/> are live (<c>!var</c>/<c>!expr</c>-capable and re-applied
	/// as they change); <see cref="Shape"/>, <see cref="Name"/> and <see cref="Mirror"/> are read once when
	/// the meshes are built. <see cref="Anchor"/> is a literal token parsed at transform time into the
	/// direction of the anchored face/corner, so it is a plain <see cref="Vector3"/> rather than a
	/// <see cref="ValueSource{T}"/>.</summary>
	public sealed record ModelPartInfo(
		ValueSource<ShapeKind> Shape,
		ValueSource<Vector3> Position,
		ValueSource<Vector3> Rotation,
		ValueSource<Vector3> Size,
		ValueSource<Color> Colour,
		ValueSource<string> Name,
		ValueSource<MirrorAxis> Mirror,
		Vector3 Anchor)
	{
		public ModelPartInfo SubstituteParameters(TransformContext ctx) =>
			this with
			{
				Shape = Shape.SubstituteParameters(ctx),
				Position = Position.SubstituteParameters(ctx),
				Rotation = Rotation.SubstituteParameters(ctx),
				Size = Size.SubstituteParameters(ctx),
				Colour = Colour.SubstituteParameters(ctx),
				Name = Name.SubstituteParameters(ctx),
				Mirror = Mirror.SubstituteParameters(ctx)
			};
	}

	/// <summary>
	/// Assembles an entity's visual from a list of primitive parts, each with its own offset, rotation,
	/// true-world size, anchor and colour. Parsing follows the <c>animation</c> <c>Steps</c> precedent:
	/// a list of maps, hand-parsed so a malformed entry names the behaviour and the offending field.
	/// </summary>
	public record ModelInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		IReadOnlyList<ModelPartInfo> Parts,
		ValueSource<Color> Colour) : BehaviourInfo(Id, Listeners)
	{
		public static ModelInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ParseParts(ctx, props, id),
				ValueSourceFactory.CreateOptionalValueSource<Color>(ctx, props.GetValueOrDefault("Colour")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ModelInfo(Id,
				substitutedListeners,
				Parts.Select(p => p.SubstituteParameters(ctx)).ToArray(),
				Colour.SubstituteParameters(ctx));

		// `Parts` is the whole behaviour — there is no single-shape shorthand (that is `primitive`'s job),
		// so an absent, mistyped or empty list is an authoring error rather than a no-op model.
		private static IReadOnlyList<ModelPartInfo> ParseParts(
			TransformContext ctx,
			IReadOnlyDictionary<string, AssemblerValue> props,
			string id)
		{
			var partsRaw = props.GetValueOrDefault("Parts");

			if (partsRaw is null or NoValue)
			{
				throw new ParsingException(
					$"model '{id}': needs a Parts list. For a single shape use the 'primitive' behaviour instead.");
			}

			if (partsRaw is not ListValue list)
			{
				throw new ParsingException($"model '{id}': Parts must be a list of part maps.");
			}

			if (list.Items.Count == 0)
			{
				throw new ParsingException($"model '{id}': Parts is empty — a model needs at least one part.");
			}

			return list.Items.Select((item, index) => item switch
			{
				DictValue d => ParsePart(ctx, d.Value, id, index),
				_ => throw new ParsingException(
					$"model '{id}' part {index}: each Parts entry must be a {{ Shape, … }} map.")
			}).ToArray();
		}

		private static ModelPartInfo ParsePart(
			TransformContext ctx,
			IReadOnlyDictionary<string, AssemblerValue> dict,
			string id,
			int index)
		{
			var shapeRaw = dict.GetValueOrDefault("Shape");

			if (shapeRaw is null or NoValue)
			{
				throw new ParsingException(
					$"model '{id}' part {index}: needs a Shape (cube, sphere, capsule, cylinder, plane, quad, "
					+ "wedge, cone, hemisphere).");
			}

			return new ModelPartInfo(
				ValueSourceFactory.CreateEnumSource(ctx, shapeRaw, ShapeKind.Cube),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, dict.GetValueOrDefault("Position")),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, dict.GetValueOrDefault("Rotation")),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, dict.GetValueOrDefault("Size")),
				ValueSourceFactory.CreateOptionalValueSource<Color>(ctx, dict.GetValueOrDefault("Colour")),
				ValueSourceFactory.CreateOptionalValueSource<string>(ctx, dict.GetValueOrDefault("Name")),
				ValueSourceFactory.CreateEnumSource(ctx, dict.GetValueOrDefault("Mirror"), MirrorAxis.None),
				ParseAnchor(dict.GetValueOrDefault("Anchor"), id, index));
		}

		// The anchor is baked at transform time (it feeds the offset maths as a plain direction), so unlike
		// the other fixed-set properties it cannot accept a !var/!parameter — say so rather than coercing.
		private static Vector3 ParseAnchor(AssemblerValue? raw, string id, int index) =>
			raw switch
			{
				null or NoValue => Vector3.zero,
				StringValue s => ModelAnchor.Parse(s.Value, $"model '{id}' part {index}"),
				_ => throw new ParsingException(
					$"model '{id}' part {index}: Anchor must be a literal token such as \"bottom-left\" — " +
					"a !var/!expr/!parameter anchor is not supported.")
			};
	}

	/// <summary>
	/// Parses a <c>model</c> part's <c>Anchor</c> token into the direction of the anchored point on the
	/// part, as a unit-per-axis <see cref="Vector3"/> (an omitted axis stays 0, i.e. centred). Tokens are
	/// hyphen-separated and order-agnostic, drawn from three independent axis vocabularies — X:
	/// <c>left</c>/<c>right</c>, Y: <c>bottom</c>/<c>top</c>, Z: <c>back</c>/<c>front</c> (+Z forward).
	/// Naming one axis twice (<c>left-right</c>) is a parse error rather than a silent last-wins.
	/// </summary>
	public static class ModelAnchor
	{
		public static Vector3 Parse(string raw, string what)
		{
			var anchor = Vector3.zero;
			var claimed = new bool[3];

			foreach (var token in raw.Split('-'))
			{
				var normalised = token.Trim().ToLowerInvariant();

				if (normalised.Length == 0)
				{
					throw new ParsingException(
						$"{what}: Anchor '{raw}' has an empty segment. Write hyphen-separated tokens, e.g. \"bottom-left\".");
				}

				var (axis, direction) = normalised switch
				{
					"left" => (0, -1f),
					"right" => (0, 1f),
					"bottom" => (1, -1f),
					"top" => (1, 1f),
					"back" => (2, -1f),
					"front" => (2, 1f),
					_ => throw new ParsingException(
						$"{what}: unknown Anchor token '{token}'. Valid tokens: left, right (X), bottom, top (Y), " +
						"back, front (Z); omit an axis to centre it.")
				};

				if (claimed[axis])
				{
					throw new ParsingException(
						$"{what}: Anchor '{raw}' names the {AxisName(axis)} axis more than once — use at most one of {AxisTokens(axis)}.");
				}

				claimed[axis] = true;
				anchor[axis] = direction;
			}

			return anchor;
		}

		private static string AxisName(int axis) =>
			axis switch { 0 => "X", 1 => "Y", _ => "Z" };

		private static string AxisTokens(int axis) =>
			axis switch { 0 => "left/right", 1 => "bottom/top", _ => "back/front" };
	}
}
