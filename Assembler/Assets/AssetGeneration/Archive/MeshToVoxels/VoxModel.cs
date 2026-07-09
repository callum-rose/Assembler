using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
    /// <summary>
    /// Mutable, dense working model for the post-processing pipeline. Steps that look
    /// at a voxel's neighbours (floaters, morphology, de-light, revolve) want random
    /// access by coordinate, which the sparse <see cref="VoxResult"/> list can't give
    /// cheaply — so the pipeline converts to this on entry and back out on export.
    ///
    /// Lives in the mesh's <b>Y-up grid space</b> (X right, Y up, Z depth), the same
    /// space as <see cref="VoxResult"/>; <see cref="VoxWriter"/> remaps to MagicaVoxel's
    /// Z-up only at write time. All pipeline steps operate here.
    ///
    /// Storage is two flat arrays of length <c>X*Y*Z</c> indexed by
    /// <c>x + X*(y + Y*z)</c>: a parallel occupancy grid and colour grid. A colour entry
    /// is only meaningful where the matching occupancy entry is <c>true</c>.
    /// </summary>
    public sealed class VoxModel
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        /// <summary>Occupancy, indexed by <see cref="Index"/>. <c>true</c> = filled.</summary>
        public bool[] Occupied { get; }

        /// <summary>Per-voxel colour, indexed by <see cref="Index"/>. Only valid where occupied.</summary>
        public Color32[] Colors { get; }

        public VoxModel(int x, int y, int z)
        {
            X = Mathf.Max(0, x);
            Y = Mathf.Max(0, y);
            Z = Mathf.Max(0, z);
            int count = X * Y * Z;
            Occupied = new bool[count];
            Colors = new Color32[count];
        }

        /// <summary>Flat array index for a grid coordinate. No bounds checking — callers gate with <see cref="InBounds"/>.</summary>
        public int Index(int x, int y, int z) => x + X * (y + Y * z);

        public bool InBounds(int x, int y, int z) =>
            x >= 0 && x < X && y >= 0 && y < Y && z >= 0 && z < Z;

        public bool IsOccupied(int x, int y, int z) =>
            InBounds(x, y, z) && Occupied[Index(x, y, z)];

        /// <summary>Decodes a flat index back to its grid coordinate (inverse of <see cref="Index"/>).</summary>
        public (int x, int y, int z) Coords(int index)
        {
            int x = index % X;
            int rem = index / X;
            return (x, rem % Y, rem / Y);
        }

        /// <summary>The six face-adjacent (6-connectivity) neighbour offsets shared by the grid steps.</summary>
        public static readonly (int dx, int dy, int dz)[] FaceNeighbours =
        {
            (1, 0, 0), (-1, 0, 0),
            (0, 1, 0), (0, -1, 0),
            (0, 0, 1), (0, 0, -1),
        };

        /// <summary>Builds a dense model from the sparse converter/quantiser output.</summary>
        public static VoxModel FromResult(VoxResult result)
        {
            var model = new VoxModel(result.GridX, result.GridY, result.GridZ);
            foreach (VoxCell cell in result.Cells)
            {
                if (!model.InBounds(cell.X, cell.Y, cell.Z))
                {
                    continue;
                }
                int i = model.Index(cell.X, cell.Y, cell.Z);
                model.Occupied[i] = true;
                model.Colors[i] = cell.Color;
            }
            return model;
        }

        /// <summary>
        /// Exports the filled voxels back to a sparse <see cref="VoxResult"/> for
        /// <see cref="VoxWriter"/>. Cells are emitted in a deterministic Z→Y→X scan so
        /// the seam round-trips reproducibly.
        /// </summary>
        public VoxResult ToResult()
        {
            var cells = new List<VoxCell>();
            for (int z = 0; z < Z; z++)
            {
                for (int y = 0; y < Y; y++)
                {
                    for (int x = 0; x < X; x++)
                    {
                        int i = Index(x, y, z);
                        if (Occupied[i])
                        {
                            cells.Add(new VoxCell(x, y, z, Colors[i]));
                        }
                    }
                }
            }
            return new VoxResult(X, Y, Z, cells);
        }
    }
}
