using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// One eye anchor on a voxel model, in the model's own grid space (the same
    /// integer space as <see cref="Assembler.Voxels.VoxelModel.Voxels"/>, so a
    /// <c>.vox</c> read straight through <see cref="Assembler.Voxels.VoxReader"/> is
    /// Z-up). <see cref="Position"/> is the surface point the eye sits on (the hit
    /// voxel centre, optionally pushed out along the normal); <see cref="Normal"/>
    /// is the outward surface direction, so a caller can orient/seat an eye decal or
    /// offset it proud of the surface.
    /// </summary>
    public sealed record EyeAnchor(Vector3 Position, Vector3 Normal);

    /// <summary>
    /// Outcome of an eye-placement run: the resolved anchors, the raw model reply, the
    /// render shown to it, the <see cref="View"/> actually used (so the caller can
    /// reproject the anchors back onto the render — it may differ from the requested view
    /// once auto-orientation kicks in), and <see cref="FrontCode"/>, the detected facing
    /// direction (null when auto-orientation was off or the front couldn't be read).
    /// <see cref="RenderPng"/> is null for the pure-geometric path, which never renders or
    /// calls the model.
    /// </summary>
    public sealed record EyePlacementResult(
        IReadOnlyList<EyeAnchor> Eyes,
        string RawResponse,
        byte[]? RenderPng,
        OrthographicView View,
        string? FrontCode,
        IReadOnlyList<OrientationCandidate> Candidates);
}
