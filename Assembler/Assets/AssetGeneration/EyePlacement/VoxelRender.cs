using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// Single entry point for turning a <see cref="VoxelModel"/> + <see cref="VoxelViewProjection"/>
    /// into a PNG: a real high-resolution camera screenshot (<see cref="VoxelCameraRenderer"/>) when a
    /// GPU is available, otherwise the deterministic CPU splat (<see cref="VoxelIsometricRenderer"/>)
    /// so it still works headless. Both honour the same projection, so a pick on either reprojects
    /// the same way.
    /// </summary>
    public static class VoxelRender
    {
        public static byte[] ToPng(VoxelModel model, VoxelViewProjection projection, int imageSize, int msaa = 8) =>
            VoxelCameraRenderer.TryRenderPng(model, projection, imageSize, msaa, out byte[] png)
                ? png
                : VoxelIsometricRenderer.RenderPng(model, projection, imageSize);
    }
}
