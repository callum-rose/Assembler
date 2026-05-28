using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using Random = Unity.Mathematics.Random;

namespace VoxelToolkit
{
	public class SnowVoxelObjectModifier : VoxelObjectModifier
	{
		[SerializeField] private int snowMaterialIndex = 1;
		[SerializeField] private int icicleMaterialIndex = 1;
		[SerializeField] private int snowCastDistance = 10;
		[SerializeField] private float coverageChance = 0.99f;
		[SerializeField] private float icicleChance = 0.05f;
		[SerializeField] private int maxIcicleLength = 4;
		[SerializeField] private uint seed = 23412;
		[SerializeField] private List<byte> materialsToIgnore = new List<byte>();
		
		public override void Apply(VoxelAsset target)
		{
			var path = new List<HierarchyNode>();
			var created = new HashSet<HierarchyNode>();
			AddSnow(target, target.HierarchyRoot, path, created, int4x4.identity);
		}

		private void AddSnow(VoxelAsset asset, HierarchyNode node, List<HierarchyNode> path, HashSet<HierarchyNode> map, int4x4 accumulatedOrientation)
		{
			if (map.Contains(node))
				return;

			var direction = math.mul(math.transpose(accumulatedOrientation), new int4(0, 1, 0, 0));

			var id = 0;
			foreach (var model in asset.Models)
				id = Mathf.Max(model.ID, id);
			
			path.Add(node);
			if (node is Group group)
			{
				for (var index = 0; index < group.ChildrenCount; index++)
					AddSnow(asset, group[index], path, map, accumulatedOrientation);
			}
			else if (node is Shape shape)
			{
				var random = new Random(seed);
				var model = shape.Animation.DefaultFrame.Value;
				var voxelObject = VoxelObject.CreateFromModel(model, asset.Palette, 32);
				var snow = new VoxelObject(voxelObject.Size.ToVector3Int(), 32);

				var size = voxelObject.Size;
				for (var x = 0; x < voxelObject.Size.x; x++)
				{
					for (var y = 0; y < voxelObject.Size.y; y++)
					{
						for (var z = 0; z < voxelObject.Size.z; z++)
						{
							var thisVoxel = voxelObject[new int3(x, y, z)];
								
							if (thisVoxel.Material == 0)
								continue;
								
							if (materialsToIgnore.Contains(thisVoxel.Material))
								continue;

							var foundSolid = false;
							for (var yc = 1; yc <= snowCastDistance; yc++)
							{
								var position = new int3(x, y, z) + direction.xyz * yc;
								var upperVoxel = math.any(position < 0) || math.any(position >= size) ? new Voxel() : voxelObject[position];
								if (upperVoxel.Material == 0)
									continue;

								foundSolid = true;
								break;
							}

							if (foundSolid)
								continue;
								
							if (random.NextFloat() > coverageChance)
								continue;
								
							snow[new int3(x, y, z)] = new Voxel((byte)snowMaterialIndex);
						}
					}
				}
					
				for (var x = 0; x < voxelObject.Size.x; x++)
				{
					for (var y = 0; y < voxelObject.Size.y; y++)
					{
						for (var z = 0; z < voxelObject.Size.z; z++)
						{
							var thisVoxel = voxelObject[new int3(x, y, z)];
							if (thisVoxel.Material == 0)
								continue;

							var icicleSize = 1 + (random.NextInt() % (maxIcicleLength - 1)); 
							var foundSolid = false;
							for (var yc = 1; yc <= icicleSize; yc++)
							{
								var position = new int3(x, y, z) - direction.xyz * yc;
								var lowerVoxel = math.any(position < 0) ? new Voxel(1) : voxelObject[position];
								if (lowerVoxel.Material == 0)
									continue;

								foundSolid = true;
								break;
							}

							if (foundSolid)
								continue;
								
							if (random.NextFloat() > icicleChance)
								continue;

							for (var yShift = 0; yShift < icicleSize; yShift++)
							{
								var position = new int3(x, y, z) - direction.xyz * (yShift + 2);
								if (math.any(position < 0) || math.any(position >= size))
									continue;
									
								snow[position] = new Voxel((byte)icicleMaterialIndex);
							}
						}
					}
				}
					
				var shapeClone = ScriptableObject.CreateInstance<Shape>();
				shapeClone.name = $"{shape.name} show";

				var lastGroup = path.FindLast(x => x is Group) as Group;
				var lastTransformation = path.FindLast(x => x is Transformation) as Transformation;

				var transformationClone = ScriptableObject.Instantiate(lastTransformation);
					
				transformationClone.name = $"{lastTransformation.name} show";

				transformationClone.Animation.AddFrame(new TransformAnimationFrame(0, math.mul(new int4x4(
						1, 0, 0, 0,
						0, 1, 0, 1,
						0, 0, 1, 0,
						0, 0, 0, 1),
					transformationClone.Animation.Frames[0].Value)));

				var updatedModel = snow.ToModel();
				updatedModel.ID = ++id;
				updatedModel.name = $"{model.name} show";
				updatedModel.ParentAsset = asset;
					
				shapeClone.AddAnimationFrame(new ModelAnimationFrame(0, updatedModel));
				transformationClone.Child = shapeClone;
				lastGroup.AddChild(transformationClone);

				map.Add(shapeClone);
					
				voxelObject.Dispose();
				snow.Dispose();
			}
			else if (node is Transformation transformation)
			{
				accumulatedOrientation = math.mul(accumulatedOrientation, transformation.Animation.Frames[0].Value);
				AddSnow(asset, transformation.Child, path, map, accumulatedOrientation);
			}
			
			path.RemoveAt(path.Count - 1);
		}
	}
}