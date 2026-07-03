using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Runtime consumption of an assembled model: builds a GameObject tree with
	/// each part's pivot as its Transform origin (local position = pivot, one
	/// voxel per world unit), attaches per-part meshes, and applies named poses
	/// as local euler rotations. Unrigged models come out as a single static child.
	/// </summary>
	public static class RigInstantiator
	{
		public static GameObject Instantiate(AssembledModel assembled, IPartMeshProvider meshes, Material? material = null)
		{
			var model = assembled.Model;
			var root = new GameObject(model.Id);
			var transforms = new Dictionary<string, Transform> { [VoxelRigModel.RootId] = root.transform };

			foreach (var part in assembled.Parts)
			{
				var go = new GameObject(part.Part.Id);
				var parent = transforms.TryGetValue(part.Part.Parent, out var t) ? t : root.transform;
				go.transform.SetParent(parent, worldPositionStays: false);
				go.transform.localPosition = part.Part.Pivot;
				transforms[part.Part.Id] = go.transform;

				if (part.Grid.Voxels.Count > 0)
				{
					var filter = go.AddComponent<MeshFilter>();
					filter.sharedMesh = meshes.BuildMesh(part.Part.Id, part.Grid);
					var renderer = go.AddComponent<MeshRenderer>();
					if (material != null)
					{
						renderer.sharedMaterial = material;
					}
				}
			}

			return root;
		}

		/// <summary>
		/// Applies a named pose: parts named in the pose get its local euler
		/// rotation, every other part resets to identity. Unknown pose names
		/// reset the whole rig.
		/// </summary>
		public static void ApplyPose(GameObject root, VoxelRigModel model, string poseName)
		{
			var pose = model.Poses.FirstOrDefault(p => p.Name == poseName) ?? Pose.Identity(poseName);
			foreach (var part in model.Parts)
			{
				var transform = FindPartTransform(root.transform, part.Id);
				if (transform != null)
				{
					transform.localRotation = pose.Rotations.TryGetValue(part.Id, out var euler)
						? Quaternion.Euler(euler)
						: Quaternion.identity;
				}
			}
		}

		private static Transform? FindPartTransform(Transform root, string partId)
		{
			if (root.name == partId)
			{
				return root;
			}

			for (var i = 0; i < root.childCount; i++)
			{
				if (FindPartTransform(root.GetChild(i), partId) is { } found)
				{
					return found;
				}
			}

			return null;
		}
	}
}
