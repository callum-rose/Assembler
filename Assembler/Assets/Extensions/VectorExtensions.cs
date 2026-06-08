
using UnityEngine;

namespace Assembler.Extensions
{
	public static class VectorExtensions
	{
		public static Quaternion FromEuler(this Vector3 euler) => Quaternion.Euler(euler);
	}
}
