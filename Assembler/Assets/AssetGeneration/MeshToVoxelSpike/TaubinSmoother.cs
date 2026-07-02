namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Stage 2 — Taubin λ|μ smoothing over the raw marching-cubes isosurface. Each pass runs a
    /// positive umbrella (uniform-Laplacian) step with weight <c>+λ</c> followed by a negative step
    /// with weight <c>−μ</c> (μ &gt; λ &gt; 0); the shrink-then-inflate pair is volume-preserving, so
    /// it removes the marching-cubes staircase without the runaway shrinkage a plain Laplacian
    /// causes. Uses the uniform (umbrella) weighting rather than the cotangent Laplacian per the
    /// handoff — the isosurface is clean g3 output, but uniform weights are cheap and staircase
    /// removal doesn't need cotangent precision.
    /// </summary>
    public static class TaubinSmoother
    {
        /// <summary>
        /// Returns a smoothed <b>copy</b> of <paramref name="mesh"/> (the original is left intact for
        /// the raw-isosurface preview). <paramref name="lambda"/> is the positive step, and the
        /// negative step uses <c>−<paramref name="mu"/></c> (pass a positive μ).
        /// </summary>
        public static g3.DMesh3 Apply(g3.DMesh3 mesh, int passes, double lambda, double mu)
        {
            var smooth = new g3.DMesh3(mesh);
            for (int p = 0; p < passes; p++)
            {
                UmbrellaStep(smooth, lambda);
                UmbrellaStep(smooth, -mu);
            }
            return smooth;
        }

        // One uniform-Laplacian (umbrella) pass: move each vertex a fraction `weight` toward the
        // average of its one-ring neighbours. Deltas are gathered from the current positions first,
        // then applied together, so the update is order-independent within a pass.
        private static void UmbrellaStep(g3.DMesh3 mesh, double weight)
        {
            var delta = new g3.Vector3d[mesh.MaxVertexID];

            foreach (int vid in mesh.VertexIndices())
            {
                g3.Vector3d p = mesh.GetVertex(vid);
                var sum = g3.Vector3d.Zero;
                int n = 0;
                foreach (int nbr in mesh.VtxVerticesItr(vid))
                {
                    sum += mesh.GetVertex(nbr);
                    n++;
                }

                delta[vid] = n > 0 ? (sum / n - p) * weight : g3.Vector3d.Zero;
            }

            foreach (int vid in mesh.VertexIndices())
            {
                mesh.SetVertex(vid, mesh.GetVertex(vid) + delta[vid]);
            }
        }
    }
}
