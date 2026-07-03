namespace Assembler.AssetGeneration.ImageOrientation
{
    /// <summary>
    /// What an orientation vision call resolved to — a closed discriminated union over the two
    /// modes the logic supports:
    /// <list type="bullet">
    /// <item><see cref="Facing"/> — a compass direction, read from a single image (the original mode).</item>
    /// <item><see cref="ViewIndex"/> — which of several candidate images best shows the front, read
    /// from a set of images and returned as a zero-based index.</item>
    /// </list>
    /// <see cref="Unrecognised"/> is the "couldn't read a valid answer" case. Match with a switch
    /// expression; the protected constructor keeps the set of cases closed.
    /// </summary>
    public abstract record OrientationAnswer
    {
        protected OrientationAnswer() { }

        /// <summary>The front faces this compass direction within the single image.</summary>
        public sealed record Facing(FacingDirection Direction) : OrientationAnswer;

        /// <summary>The best view is the candidate image at this zero-based index.</summary>
        public sealed record ViewIndex(int Index) : OrientationAnswer;

        /// <summary>The reply contained no recognisable answer.</summary>
        public sealed record Unrecognised : OrientationAnswer;
    }
}
