using System.Collections.Generic;
using Assembler.Voxels.Scripting;
using UnityEngine;

namespace Assembler.Voxels.Terrain
{
	/// <summary>How the base layer derives a surface height for each (x, y) column.</summary>
	public enum BaseKind
	{
		Flat,
		Noise,
	}

	/// <summary>Which noise combiner drives a <see cref="BaseKind.Noise"/> base.</summary>
	public enum NoiseKind
	{
		Fbm,
		Ridged,
		Billow,
	}

	/// <summary>Convenience walls/ceiling wrapped around the bounded volume.</summary>
	public enum Enclosure
	{
		Open,
		Walled,
		Sealed,
	}

	/// <summary>Whether a modifier op adds material (<see cref="Stamp"/>) or removes it (<see cref="Carve"/>).</summary>
	public enum OpKind
	{
		Stamp,
		Carve,
	}

	/// <summary>The primitive a modifier op rasterises.</summary>
	public enum ShapeKind
	{
		Box,
		Sphere,
		Cylinder,
		Cone,
	}

	/// <summary>
	/// How a <see cref="OpKind.Stamp"/> reconciles with existing voxels.
	/// <see cref="Add"/> only fills empty cells (preserving the base colour where it
	/// already exists); the others overwrite. <see cref="Max"/>/<see cref="Min"/> are
	/// reserved for future height-aware blending and currently behave as
	/// <see cref="Replace"/>.
	/// </summary>
	public enum CombineMode
	{
		Add,
		Max,
		Min,
		Replace,
	}

	/// <summary>Octave parameters for a noise-driven base height field.</summary>
	public sealed record NoiseSettings(
		NoiseKind Kind,
		int Octaves,
		float Frequency,
		float Amplitude,
		float Lacunarity,
		float Gain,
		float DomainWarp);

	/// <summary>
	/// The single base op: a flat floor at <see cref="BaseHeight"/>, or a noise field
	/// rising <see cref="NoiseSettings.Amplitude"/> voxels above it.
	/// </summary>
	public sealed record BaseOp(
		BaseKind Type,
		Color32 Colour,
		int BaseHeight,
		NoiseSettings? Noise);

	/// <summary>
	/// One ordered modifier: stamp or carve a primitive. Box uses
	/// <see cref="Min"/>/<see cref="Max"/>; sphere/cylinder/cone use
	/// <see cref="Centre"/>/<see cref="Radius"/> (+ <see cref="Height"/>/<see cref="Axis"/>).
	/// </summary>
	public sealed record ModifierOp(
		OpKind Op,
		ShapeKind Shape,
		Color32 Colour,
		CombineMode Combine,
		Vector3Int Min,
		Vector3Int Max,
		Vector3Int Centre,
		int Radius,
		int Height,
		VoxelAxis Axis);

	/// <summary>
	/// A complete, immutable terrain recipe: a bounded Z-up volume, one base op, an
	/// ordered modifier stack, and a convenience enclosure. Consumed by
	/// <see cref="TerrainGenerator"/> to produce a <see cref="VoxelModel"/>.
	/// </summary>
	public sealed record TerrainSpec(
		string Name,
		int Seed,
		Vector3Int Size,
		int SkinThickness,
		Enclosure Enclosure,
		int WallHeight,
		int WallThickness,
		Color32 WallColour,
		BaseOp Base,
		IReadOnlyList<ModifierOp> Ops)
	{
		/// <summary>
		/// Returns a copy with the editor-tweakable fields overridden. Lives here (in
		/// the runtime assembly, where <c>IsExternalInit</c> exists) so the editor can
		/// apply UI overrides without depending on init-accessor support itself.
		/// </summary>
		public TerrainSpec With(string name, int seed, Vector3Int size, Enclosure enclosure)
			=> this with { Name = name, Seed = seed, Size = size, Enclosure = enclosure };
	}
}
