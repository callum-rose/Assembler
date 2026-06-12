using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// One named part in a model's rig tree. <see cref="Pivot"/> is the joint
	/// position expressed in the PARENT's local frame; the part's own local
	/// origin sits at that pivot. World transform is the composition of pivot
	/// translations up the chain (plus pose rotations at runtime).
	/// </summary>
	public sealed record VoxelPart
	{
		public string Id { get; init; } = string.Empty;
		public string Parent { get; init; } = VoxelRigModel.RootId;
		public Vector3Int Pivot { get; init; }

		/// <summary>
		/// Loose connectivity: the part may contain disconnected chunks
		/// (foliage, scatter, debris) without failing validation. A floating
		/// leaf is fine; a floating hand is not — the planner decides per part.
		/// </summary>
		public bool Loose { get; init; }

		public PartData Data { get; init; } = new PlannedPartData(PartEncoding.Layers, Vector3Int.one, Vector3Int.zero, string.Empty);
	}

	/// <summary>A named pose: part id → local euler rotation in degrees.</summary>
	public sealed record Pose(string Name, IReadOnlyDictionary<string, Vector3> Rotations)
	{
		public static Pose Identity(string name) => new(name, new Dictionary<string, Vector3>());
	}

	/// <summary>
	/// The core part-based voxel model format (*.vmodel.yaml). Coordinates are
	/// Claude-facing Y-up throughout; storage exports (.vox / Goxel text) are
	/// Z-up via an involutive y/z swap at the boundary.
	/// </summary>
	public sealed record VoxelRigModel
	{
		public const string RootId = "root";

		public string Id { get; init; } = string.Empty;
		public int Version { get; init; } = 1;
		public bool Rigged { get; init; }

		/// <summary>Binding theming from the manifest (colours, materials, style); empty when none was given.</summary>
		public string Description { get; init; } = string.Empty;

		/// <summary>Symmetry contract from the manifest: bilateral | radial:N | none. Bilateral models must mirror across their centre x plane.</summary>
		public string Symmetry { get; init; } = "none";

		public bool IsBilateral => Symmetry == "bilateral";

		/// <summary>Metres per voxel — the set-wide scale anchor.</summary>
		public float Unit { get; init; } = 0.1f;
		public float RealWorldHeight { get; init; }

		/// <summary>Target bounding-box extent in voxels along z, the FORWARD axis (nose-to-tail). 0 = unconstrained.</summary>
		public int TargetLength { get; init; }

		/// <summary>Target bounding-box extent in voxels along x, the left-right axis. 0 = unconstrained.</summary>
		public int TargetWidth { get; init; }

		/// <summary>± voxels each specified bounding-box extent may deviate (plan gate and validator).</summary>
		public int SizeTolerance { get; init; } = 1;

		/// <summary>Placement anchor / ground contact point (e.g. feet_center).</summary>
		public string Origin { get; init; } = "feet_center";

		public IReadOnlyList<PaletteEntry> Palette { get; init; } = Array.Empty<PaletteEntry>();
		public IReadOnlyList<VoxelPart> Parts { get; init; } = Array.Empty<VoxelPart>();
		public IReadOnlyList<Pose> Poses { get; init; } = Array.Empty<Pose>();

		public int HeightInVoxels => Mathf.Max(1, Mathf.RoundToInt(RealWorldHeight / Mathf.Max(1e-6f, Unit)));

		public VoxelPart? FindPart(string id) => Parts.FirstOrDefault(p => p.Id == id);

		/// <summary>Replaces one part's data, preserving declaration order.</summary>
		public VoxelRigModel WithPartData(string partId, PartData data) => this with
		{
			Parts = Parts.Select(p => p.Id == partId ? p with { Data = data } : p).ToArray(),
		};

		/// <summary>Replaces a whole part (matched by id), preserving declaration order.</summary>
		public VoxelRigModel WithPart(VoxelPart part) => this with
		{
			Parts = Parts.Select(p => p.Id == part.Id ? part : p).ToArray(),
		};

		/// <summary>The shared 1-based palette every part grid indexes into.</summary>
		public Color32[] ToPalette() => Palette.Select(e => e.Colour).ToArray();

		/// <summary>1-based palette index for a key, or 0 if unknown.</summary>
		public byte PaletteIndexOf(char key)
		{
			for (var i = 0; i < Palette.Count; i++)
			{
				if (Palette[i].Key == key)
				{
					return (byte)(i + 1);
				}
			}

			return 0;
		}
	}
}
