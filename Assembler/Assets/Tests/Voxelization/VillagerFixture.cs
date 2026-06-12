using System.Collections.Generic;
using Assembler.Voxelization;
using UnityEngine;
using Pose = Assembler.Voxelization.Pose;

namespace Tests.Voxelization
{
	/// <summary>
	/// The hand-authored M0/M1 acceptance fixture: a 10-voxel-tall rigged
	/// villager (legs + torso + head + mirrored arms + wave pose), fully
	/// connected and exactly on scale at unit 0.18 / height 1.8m.
	/// </summary>
	public static class VillagerFixture
	{
		public static VoxelRigModel Build()
		{
			var palette = new[]
			{
				new PaletteEntry('S', new Color32(0xe0, 0xb0, 0x80, 0xff)),
				new PaletteEntry('B', new Color32(0x3a, 0x5f, 0xcd, 0xff)),
				new PaletteEntry('K', new Color32(0x22, 0x22, 0x22, 0xff)),
			};

			var torsoLayer = "BBB\nBBB";
			var headLayer = "SSS\nSSS";

			var parts = new[]
			{
				new VoxelPart
				{
					Id = "leg.L",
					Parent = VoxelRigModel.RootId,
					Pivot = new Vector3Int(-1, 0, 0),
					Data = new LayersPartData(new Vector3Int(1, 4, 1), Vector3Int.zero, new[] { "K", "K", "K", "K" }),
				},
				new VoxelPart
				{
					Id = "leg.R",
					Parent = VoxelRigModel.RootId,
					Pivot = new Vector3Int(1, 0, 0),
					Data = new MirrorPartData("leg.L", MirrorAxis.X),
				},
				new VoxelPart
				{
					Id = "torso",
					Parent = VoxelRigModel.RootId,
					Pivot = new Vector3Int(0, 4, 0),
					Data = new LayersPartData(
						new Vector3Int(3, 4, 2),
						new Vector3Int(-1, 0, -1),
						new[] { torsoLayer, torsoLayer, torsoLayer, torsoLayer }),
				},
				new VoxelPart
				{
					Id = "head",
					Parent = "torso",
					Pivot = new Vector3Int(0, 4, 0),
					Data = new LayersPartData(
						new Vector3Int(3, 2, 2),
						new Vector3Int(-1, 0, -1),
						new[] { headLayer, headLayer }),
				},
				new VoxelPart
				{
					Id = "arm.L",
					Parent = "torso",
					Pivot = new Vector3Int(-2, 3, 0),
					Data = new LayersPartData(
						new Vector3Int(1, 4, 1),
						new Vector3Int(0, -3, 0),
						new[] { "S", "B", "B", "B" }),
				},
				new VoxelPart
				{
					Id = "arm.R",
					Parent = "torso",
					Pivot = new Vector3Int(2, 3, 0),
					Data = new MirrorPartData("arm.L", MirrorAxis.X),
				},
			};

			return new VoxelRigModel
			{
				Id = "villager",
				Rigged = true,
				Symmetry = "bilateral",
				Unit = 0.18f,
				RealWorldHeight = 1.8f,
				Origin = "feet_center",
				Palette = palette,
				Parts = parts,
				Poses = new[]
				{
					Pose.Identity("idle"),
					new Pose("wave", new Dictionary<string, Vector3> { ["arm.R"] = new(0f, 0f, -160f) }),
				},
			};
		}
	}
}
