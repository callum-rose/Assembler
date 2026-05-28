using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit
{
	public static class TypeConverter
	{
		public static int3 ToInt3(this Vector3Int value) => new int3(value.x, value.y, value.z);
		public static Vector3Int ToVector3Int(this int3 value) => new Vector3Int(value.x, value.y, value.z);
	}
}