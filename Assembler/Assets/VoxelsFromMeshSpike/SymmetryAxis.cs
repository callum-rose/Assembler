namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// A grid axis in the Y-up <see cref="VoxResult"/> space, shared by the opt-in symmetry
    /// steps (<see cref="Mirror"/>, <see cref="Revolve"/>). <see cref="X"/> is left/right — the
    /// usual bilateral plane for a creature/prop — so it is the mirror default; <see cref="Y"/>
    /// is up, the usual spin axle for a standalone wheel, so it is the revolve default.
    /// </summary>
    public enum SymmetryAxis
    {
        X,
        Y,
        Z,
    }
}
