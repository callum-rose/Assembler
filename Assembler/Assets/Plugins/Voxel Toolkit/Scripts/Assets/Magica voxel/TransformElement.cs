using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit.MagicaVoxel
{
	public class TransformElement : HierarchyElement
	{
		public int ChildId;
		public int LayerId;
		public Dictionary<string, string>[] Frames;
		public HierarchyElement Child;

		public TransformElement(int id, int childId, int layerId, Dictionary<string, string>[] frames, Dictionary<string, string> attributes) : base(id, attributes)
		{
			ChildId = childId;
			LayerId = layerId;
			Frames = frames;
		}

		public override HierarchyNode AddToAsset(VoxelAsset asset, List<Model> models)
		{
			var transformation = ScriptableObject.CreateInstance<Transformation>();
			var looped = Attributes.TryGetValue("_loop", out var loopValue) && loopValue == "1";
			transformation.Name = Attributes.TryGetValue("_name", out string transformationName) ? transformationName : null;
			transformation.Layer = asset.FindLayerById(LayerId);
			transformation.Animation.Looped = looped;
            
			transformation.Child = Child?.AddToAsset(asset, models);

			for (var index = 0; index < Frames.Length; index++)
			{
				var serializedFrame = Frames[index];
				if (!serializedFrame.TryGetValue("_t", out string serializedTranslation))
					serializedTranslation = "0 0 0";
                
				var splitedTranslation = Array.ConvertAll(serializedTranslation.Split(' '),
					x => int.Parse(x, CultureInfo.InvariantCulture));

				var rotation = int3x3.identity;

				if (serializedFrame.TryGetValue("_r", out string serializedRotation))
				{
					rotation = new int3x3();
					var integer = int.Parse(serializedRotation);
					var firstTwo = integer & 3;
					var secondTwo = (integer & (3 << 2)) >> 2;
					var forth = integer & 16;
					var fifth = integer & 32;
					var sixth = integer & 64;
					var third = 0;

					rotation[firstTwo][0] = forth == 0 ? 1 : -1;
					rotation[secondTwo][1] = fifth == 0 ? 1 : -1;

					var firstMask = (1 << firstTwo);
					var secondMask = (1 << secondTwo);
					
					var mask = 0xFFFFFFFF ^ firstMask ^ secondMask;
					var invertedMask = (uint)~mask;
					third = (int)math.log2(7 - invertedMask);
					rotation[third][2] = sixth == 0 ? 1 : -1;
				}

				var position = new int4(splitedTranslation[0], splitedTranslation[1], splitedTranslation[2], 1);
				
				var transformMatrix = new int4x4(
						new int4(1, 0, 0, 0),
						new int4(0, 0, 1, 0),
						new int4(0, 1, 0, 0),
						new int4(0, 0, 0, 1)
					);

				var full = new int4x4(
					new int4(rotation.c0, 0),
					new int4(rotation.c1, 0),
					new int4(rotation.c2, 0),
					position);

				full = math.mul(full, transformMatrix);
				full = math.mul(transformMatrix, full);
				
				if (!serializedFrame.TryGetValue("_f", out var frameIndex))
					frameIndex = "0";

				var parsedFrameIndex = int.Parse(frameIndex);
				transformation.AddAnimationFrame(new TransformAnimationFrame(parsedFrameIndex, full));
			}
			
			return transformation;
		}
	}
}