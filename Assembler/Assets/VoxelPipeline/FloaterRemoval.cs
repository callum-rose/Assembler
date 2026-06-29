using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.VoxelPipeline
{
    /// <summary>
    /// Pipeline step 2 — deletes floating voxels (voxelization specks) by connected-component
    /// analysis over the occupancy grid (6-connectivity). Components smaller than a size
    /// threshold are removed; larger ones are kept.
    ///
    /// Deliberately <b>not</b> largest-component-only: that nukes intended detached pieces
    /// (an antenna ball, an ear tip). The threshold spares substantial detached parts while
    /// killing the specks.
    /// </summary>
    public static class FloaterRemoval
    {
        public readonly struct Options
        {
            /// <summary>Absolute floor: a component with fewer voxels than this is removed.</summary>
            public int MinVoxels { get; }

            /// <summary>...and at least this fraction (0..1) of all filled voxels.</summary>
            public float MinFraction { get; }

            public Options(int minVoxels, float minFraction)
            {
                MinVoxels = Mathf.Max(1, minVoxels);
                MinFraction = Mathf.Clamp01(minFraction);
            }

            public static Options Default => new Options(2, 0.005f);
        }

        public static void Apply(VoxModel model, Options options)
        {
            int total = 0;
            foreach (bool occupied in model.Occupied)
            {
                if (occupied)
                {
                    total++;
                }
            }
            if (total == 0)
            {
                return;
            }

            // component[i] == 0 means "not yet assigned"; ids start at 1.
            var component = new int[model.Occupied.Length];
            var sizes = new List<int> { 0 }; // index 0 unused so ids line up
            int nextId = 1;
            var stack = new Stack<int>();

            for (int seed = 0; seed < model.Occupied.Length; seed++)
            {
                if (!model.Occupied[seed] || component[seed] != 0)
                {
                    continue;
                }

                int id = nextId++;
                int size = 0;
                component[seed] = id;
                stack.Push(seed);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    size++;
                    (int x, int y, int z) = model.Coords(i);
                    foreach ((int dx, int dy, int dz) in VoxModel.FaceNeighbours)
                    {
                        int nx = x + dx, ny = y + dy, nz = z + dz;
                        if (!model.InBounds(nx, ny, nz))
                        {
                            continue;
                        }
                        int n = model.Index(nx, ny, nz);
                        if (model.Occupied[n] && component[n] == 0)
                        {
                            component[n] = id;
                            stack.Push(n);
                        }
                    }
                }
                sizes.Add(size);
            }

            int threshold = Mathf.Max(options.MinVoxels, Mathf.CeilToInt(options.MinFraction * total));
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                int id = component[i];
                if (id != 0 && sizes[id] < threshold)
                {
                    model.Occupied[i] = false;
                }
            }
        }
    }
}
