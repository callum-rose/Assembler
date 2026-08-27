using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>
	/// Assembles the entity's visual from a list of primitive parts — each with its own offset, rotation,
	/// size, anchor and colour — as mesh children of the entity, so a multi-shape prop is one entity
	/// instead of one entity per piece. Use this whenever an entity needs more than one shape; use
	/// <c>primitive</c> for exactly one. Never stack repeated <c>primitive</c> behaviours on one entity —
	/// they all render at the origin, axis-aligned, on top of each other. A part's <c>Size</c> is its true
	/// world bounding box, unlike <c>primitive</c>'s <c>Size</c>, which is a raw localScale: a cylinder of
	/// Size 1,3,1 here is genuinely 3 units tall and a plane of Size 10,1,10 is genuinely 10 by 10, because
	/// Unity's native cylinder/capsule (2 units tall) and plane (10 by 10) scales are divided out for you.
	/// Anchors move the pivot off the part's centre so a part at Position 0,0,0 with Anchor bottom sits on
	/// the origin rather than half-buried, and Rotation then pivots about that anchor. Mirror emits
	/// reflected duplicates of a part, so a symmetric shape is authored once. Parts are visual only — for
	/// collision, add a <c>box collider</c> or <c>sphere collider</c> with <c>Fit: bounds</c> (one collider
	/// around the whole model) or <c>Fit: parts</c> (one per part), listed after this behaviour, instead of
	/// hand-writing a Size or Radius.
	/// </summary>
	/// <remarks>
	/// Properties:
	///   Parts: Ordered list of part maps (required — there is no single-shape shorthand). Each has Shape (required — cube, sphere, capsule, cylinder, plane or quad), Position (offset from the entity origin, default 0,0,0), Rotation (euler angles about the part's anchor, default 0,0,0), Size (true world dimensions, default 1,1,1), Anchor (hyphen-separated, order-agnostic point on the part that lands on Position — left/right on X, bottom/top on Y, back/front on Z, e.g. "bottom-left"; an omitted axis is centred and naming one axis twice is an error), Colour (overrides the model-wide Colour for this part), Mirror (emit reflected duplicates in addition to the original — x, z, or xz for all three twins) and Name (hierarchy name, defaults to "Part {i} ({Shape})").
	///   Colour: Model-wide tint applied to every part that does not set its own Colour; omit both and parts keep the shared material's colour.
	/// </remarks>
	public class Model : GameBehaviour<ModelData>, INeedsLiveProperties
	{
		// URP's Lit shader exposes the main colour as _BaseColor.
		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

		public LivePropertyUpdater LiveProperties { get; set; } = null!;

		protected override void OnInitialise(ModelData data)
		{
			for (int i = 0; i < data.Parts.Count; i++)
			{
				var part = data.Parts[i];

				// Shape, Name and Mirror decide how many GameObjects exist and which mesh each holds, so
				// they are read once here rather than bound — a live shape would mean rebuilding the child.
				// Shape is required (ModelInfo rejects a part without one), so it is read outright: a null
				// provider here is a pipeline bug and should throw rather than quietly become a cube.
				var shape = part.Shape.Get();
				var name = part.Name.ValueOr($"Part {i} ({shape})");
				var mirror = part.Mirror.ValueOr(MirrorAxis.None);

				// Colour fallback chain, rung by rung: the part's own colour, else the model-wide colour,
				// else (both being omitted) nothing is bound and the shared material shows through. Picking
				// the provider rather than ValueOr's value keeps a !var/!expr colour live.
				var colour = part.Colour.Or(data.Colour);

				foreach (var twin in ModelGeometry.Twins(mirror))
				{
					BuildPart(part, shape, name + twin.NameSuffix, twin, colour);
				}
			}
		}

		private void BuildPart(ModelPart part, PrimitiveType shape, string name, MirrorTwin twin,
			IValueProvider<Color> colour)
		{
			var mesh = GameObject.CreatePrimitive(shape);
			mesh.name = name;
			mesh.transform.SetParent(transform, false);

			// Drop the collider CreatePrimitive adds: model parts are visual, collision is declared
			// explicitly. DestroyImmediate when not playing so the edit-mode sandbox build (which
			// instantiates without entering play mode) can strip it too — plain Destroy throws in edit mode.
			if (mesh.TryGetComponent<Collider>(out var collider))
			{
#if UNITY_EDITOR
				if (Application.isPlaying)
				{
#endif
					Destroy(collider);
#if UNITY_EDITOR
				}
				else
				{
					DestroyImmediate(collider);
				}
#endif
			}

			var renderer = mesh.GetComponent<MeshRenderer>();
			renderer.sharedMaterial = Resources.Load<Material>("Materials/Primitive");

			// Position, Rotation and Size cannot be three independent one-line bindings: the anchor offset is
			// a function of the *current* Size, so all three share cached state and re-apply together.
			var placement = new PartPlacement(mesh.transform, shape, part.Anchor, twin);
			part.Position.BindLive(this, placement.SetPosition, Vector3.zero);
			part.Rotation.BindLive(this, placement.SetRotation, Vector3.zero);
			part.Size.BindLive(this, placement.SetSize, Vector3.one);

			// The no-fallback overload: with neither a part nor a model colour, nothing is applied and the
			// part keeps the shared material's own colour — the last rung of the fallback chain.
			var block = new MaterialPropertyBlock();
			colour.BindLive(this, c =>
			{
				block.SetColor(BaseColorId, c);
				renderer.SetPropertyBlock(block);
			});
		}

		// Holds one mesh child's live position/rotation/size and re-derives the whole transform whenever any
		// of them changes. Each setter is a BindLive sink, so an intermediate apply during construction is
		// harmless — the last binding to land leaves the final transform.
		private sealed class PartPlacement
		{
			private readonly Transform _transform;
			private readonly PrimitiveType _shape;
			private readonly Vector3 _anchor;
			private readonly MirrorTwin _twin;

			private Vector3 _position = Vector3.zero;
			private Vector3 _rotation = Vector3.zero;
			private Vector3 _size = Vector3.one;

			public PartPlacement(Transform transform, PrimitiveType shape, Vector3 anchor, MirrorTwin twin) =>
				(_transform, _shape, _anchor, _twin) = (transform, shape, anchor, twin);

			public void SetPosition(Vector3 position)
			{
				_position = position;
				Apply();
			}

			public void SetRotation(Vector3 rotation)
			{
				_rotation = rotation;
				Apply();
			}

			public void SetSize(Vector3 size)
			{
				_size = size;
				Apply();
			}

			// The anchor offset is applied *through* the rotation (rotated by it), so Rotation pivots the
			// part about its anchor — a leaning fence post turns at its foot, not at its middle.
			private void Apply()
			{
				var rotation = Quaternion.Euler(Vector3.Scale(_rotation, _twin.Rotation));

				_transform.localScale = ModelGeometry.Normalise(_shape, _size);
				_transform.localRotation = rotation;
				_transform.localPosition = Vector3.Scale(_position, _twin.Position)
					+ rotation * ModelGeometry.AnchorOffset(Vector3.Scale(_anchor, _twin.Anchor), _size);
			}
		}
	}

	/// <summary>One emitted copy of a <c>model</c> part: the sign each of position, rotation and anchor is
	/// scaled by, and the suffix its hierarchy name carries. <see cref="ModelGeometry.Original"/> is the
	/// identity copy every part emits.</summary>
	public sealed record MirrorTwin(Vector3 Position, Vector3 Rotation, Vector3 Anchor, string NameSuffix);

	/// <summary>
	/// The geometry a <c>model</c> is assembled from, separated out so it can be unit-tested (and reused for
	/// collider fitting) without building GameObjects.
	/// </summary>
	public static class ModelGeometry
	{
		/// <summary>The un-mirrored copy: every sign 1, no name suffix.</summary>
		public readonly static MirrorTwin Original = new(Vector3.one, Vector3.one, Vector3.one, string.Empty);

		private readonly static MirrorTwin MirrorX =
			new(new Vector3(-1f, 1f, 1f), new Vector3(1f, -1f, -1f), new Vector3(-1f, 1f, 1f), " (mirrored x)");

		private readonly static MirrorTwin MirrorZ =
			new(new Vector3(1f, 1f, -1f), new Vector3(-1f, -1f, 1f), new Vector3(1f, 1f, -1f), " (mirrored z)");

		// Composing X then Z: positions negate on both axes, and the two rotation flips cancel on Y.
		private readonly static MirrorTwin MirrorXZ =
			new(new Vector3(-1f, 1f, -1f), new Vector3(-1f, 1f, -1f), new Vector3(-1f, 1f, -1f), " (mirrored xz)");

		/// <summary>
		/// Converts a part's true world <paramref name="size"/> into the localScale that produces it. Unity's
		/// native primitives are not all unit-sized: a cylinder and a capsule are 2 units tall and a plane is
		/// 10 by 10, so those axes are divided out. Cube, sphere and quad are already unit-sized.
		/// </summary>
		public static Vector3 Normalise(PrimitiveType shape, Vector3 size) =>
			shape switch
			{
				PrimitiveType.Cylinder or PrimitiveType.Capsule => new Vector3(size.x, size.y * 0.5f, size.z),
				PrimitiveType.Plane => new Vector3(size.x * 0.1f, size.y, size.z * 0.1f),
				_ => size
			};

		/// <summary>
		/// The local offset that moves a centre-pivoted mesh so that the point named by
		/// <paramref name="anchor"/> — a per-axis direction, 0 for a centred axis — lands on the part's
		/// Position. Anchoring the bottom of a 2-tall part lifts its centre by 1.
		/// </summary>
		public static Vector3 AnchorOffset(Vector3 anchor, Vector3 size) =>
			Vector3.Scale(-anchor, size) * 0.5f;

		/// <summary>The copies one part emits: always the original, plus a reflected twin per mirrored axis
		/// (<c>xz</c> emits three — the X mirror, the Z mirror, and both).</summary>
		public static IReadOnlyList<MirrorTwin> Twins(MirrorAxis mirror) =>
			mirror switch
			{
				MirrorAxis.X => new[] { Original, MirrorX },
				MirrorAxis.Z => new[] { Original, MirrorZ },
				MirrorAxis.XZ => new[] { Original, MirrorX, MirrorZ, MirrorXZ },
				_ => new[] { Original }
			};
	}
}
