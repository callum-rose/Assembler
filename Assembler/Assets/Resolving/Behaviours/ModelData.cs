using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>One resolved <c>model</c> part. <see cref="Position"/>, <see cref="Rotation"/>,
	/// <see cref="Size"/> and <see cref="Colour"/> are bound live when the meshes are built;
	/// <see cref="Shape"/>, <see cref="Name"/> and <see cref="Mirror"/> are read once. An omitted optional
	/// is a <see cref="NullValueProvider{T}"/>, which is what selects the model-wide colour for
	/// <see cref="Colour"/> and the geometric defaults for the rest.</summary>
	public sealed class ModelPart
	{
		public IValueProvider<PrimitiveType> Shape { get; }
		public IValueProvider<Vector3> Position { get; }
		public IValueProvider<Vector3> Rotation { get; }
		public IValueProvider<Vector3> Size { get; }
		public IValueProvider<Color> Colour { get; }
		public IValueProvider<string> Name { get; }
		public IValueProvider<MirrorAxis> Mirror { get; }
		public Vector3 Anchor { get; }

		public ModelPart(
			IValueProvider<PrimitiveType> shape,
			IValueProvider<Vector3> position,
			IValueProvider<Vector3> rotation,
			IValueProvider<Vector3> size,
			IValueProvider<Color> colour,
			IValueProvider<string> name,
			IValueProvider<MirrorAxis> mirror,
			Vector3 anchor)
		{
			Shape = shape;
			Position = position;
			Rotation = rotation;
			Size = size;
			Colour = colour;
			Name = name;
			Mirror = mirror;
			Anchor = anchor;
		}
	}

	public sealed class ModelData : BehaviourData
	{
		public IReadOnlyList<ModelPart> Parts { get; }
		public IValueProvider<Color> Colour { get; }

		public ModelData(string id, IReadOnlyList<ModelPart> parts, IValueProvider<Color> colour) : base(id) =>
			(Parts, Colour) = (parts, colour);
	}

	public static class ModelPartResolver
	{
		/// <summary>Resolves one parsed part into its runtime form, turning each value source into a live provider.</summary>
		public static ModelPart Resolve(this ModelPartInfo part, ResolutionContext ctx) =>
			new(part.Shape.Resolve(ctx),
				part.Position.Resolve(ctx),
				part.Rotation.Resolve(ctx),
				part.Size.Resolve(ctx),
				part.Colour.Resolve(ctx),
				part.Name.Resolve(ctx),
				part.Mirror.Resolve(ctx),
				part.Anchor);
	}
}
