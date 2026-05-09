using Assembler.Parsing.Phase2.Parsing.Phase2;

namespace AssemblerAlpha.Core
{
	public static class VectorExtensions
	{
		public static UnityEngine.Vector3 ToUnity(this Vector3 vector)
		{
			return new UnityEngine.Vector3(vector.X, vector.Y, vector.Z);
		}
	}
}