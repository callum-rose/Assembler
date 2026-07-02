namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Stage 6 (optional) — nudge each smoothed vertex back onto the SDF iso=0 surface. Taubin
    /// smoothing preserves volume but can drift vertices slightly off the true isosurface; a couple
    /// of Newton steps along the trilinear SDF gradient re-seat them on <c>f = 0</c> without
    /// undoing the smoothing. The gradient is the field's own analytic (central-difference) one, so
    /// no numeric differencing is needed here.
    /// </summary>
    public static class SurfaceReprojection
    {
        private const int Iterations = 2;

        /// <summary>Reproject <paramref name="mesh"/>'s vertices onto the zero level set of <paramref name="field"/> in place.</summary>
        public static void Apply(g3.DMesh3 mesh, g3.DenseGridTrilinearImplicit field)
        {
            foreach (int vid in mesh.VertexIndices())
            {
                g3.Vector3d p = mesh.GetVertex(vid);
                for (int i = 0; i < Iterations; i++)
                {
                    double value = field.Value(ref p);
                    g3.Vector3d grad = field.Gradient(ref p);
                    double len2 = grad.LengthSquared;
                    if (len2 < 1e-12)
                    {
                        break;
                    }

                    // Newton step onto the level set: p ← p − f·∇f / |∇f|².
                    p -= grad * (value / len2);
                }
                mesh.SetVertex(vid, p);
            }
        }
    }
}
