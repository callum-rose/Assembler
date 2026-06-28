using System;
using System.IO;
using Assembler.Voxels.Scripting;
using Assembler.Voxels.Terrain;
using UnityEditor;
using UnityEngine;

namespace Assembler.Voxels.Editor.Terrain
{
	/// <summary>
	/// Validates a <see cref="TerrainSpec"/>, generates its model, and writes a
	/// MagicaVoxel <c>.vox</c> into the project. Dropping it under
	/// <c>Assets/Resources/Voxels/</c> lets the Voxel Toolkit's scripted importer
	/// auto-convert it to a Mesh on the following <see cref="AssetDatabase.Refresh()"/>.
	/// </summary>
	public static class TerrainBaker
	{
		public const string DefaultOutputFolder = "Assets/Resources/Voxels";

		/// <summary>Generates and writes the <c>.vox</c>, returning the asset path.</summary>
		public static string Bake(TerrainSpec spec, VoxelScriptLimits limits, string outputFolder)
		{
			ValidateSize(spec.Size);
			var model = TerrainGenerator.Generate(spec, limits);
			var bytes = VoxWriter.Write(model);

			Directory.CreateDirectory(outputFolder);
			var path = Path.Combine(outputFolder, spec.Name + ".vox");
			File.WriteAllBytes(path, bytes);
			AssetDatabase.Refresh();
			return path;
		}

		/// <summary>
		/// Guards the 256-per-axis <c>.vox</c> coordinate limit up front with a clear
		/// message, rather than letting <see cref="VoxWriter"/> throw a raw exception.
		/// </summary>
		public static void ValidateSize(Vector3Int size)
		{
			if (size.x < 1 || size.y < 1 || size.z < 1)
			{
				throw new InvalidOperationException(
					$"Terrain Size must be at least 1 on every axis (got {size.x}x{size.y}x{size.z}).");
			}

			if (size.x > 256 || size.y > 256 || size.z > 256)
			{
				throw new InvalidOperationException(
					$"Terrain Size {size.x}x{size.y}x{size.z} exceeds the 256-per-axis .vox limit. Reduce Size.");
			}
		}
	}
}
