namespace Assembler.Voxels.Scripting
{
	/// <summary>
	/// Names a single coordinate axis. Used by <see cref="VoxelBuilder"/> for
	/// orientation-bearing primitives (cylinders, mirrors, plane fills). The
	/// expression compiler resolves <c>VoxelAxis.X</c> etc. as static fields, so
	/// the type is registered with the compiler before a script runs.
	/// </summary>
	public enum VoxelAxis
	{
		X,
		Y,
		Z,
	}
}
