using System;
using System.Collections.Generic;
using System.Linq;

namespace Assembler.AssetGeneration.ImageOrientation
{
    /// <summary>
    /// The direction the front of an object faces within the 2D plane of an image,
    /// expressed as one of eight compass-style codes (L, R, U, D and the four
    /// diagonals). Left/Right/Up/Down are relative to the image edges as seen by
    /// the viewer, so a car whose front points at the bottom-left corner is
    /// <see cref="LeftDown"/> (code "LD").
    /// </summary>
    public enum FacingDirection
    {
        Left,
        Right,
        Up,
        Down,
        LeftUp,
        LeftDown,
        RightUp,
        RightDown,
    }

    /// <summary>
    /// How a model reply was classified: a recognised direction, a deliberate
    /// "I can't tell" from the model, or an unrecognisable reply.
    /// </summary>
    public enum OrientationOutcome
    {
        /// <summary>A facing direction was recognised (see <see cref="FacingDirection"/>).</summary>
        Resolved,

        /// <summary>The model explicitly reported it could not tell which way the front faces.</summary>
        Unsure,

        /// <summary>The reply contained no code or sentinel we could recognise.</summary>
        Unrecognised,
    }

    public static class FacingDirectionExtensions
    {
        /// <summary>Sentinel the model is asked to reply with when it cannot tell the facing direction.</summary>
        public const string UnsureCode = "UNSURE";

        // Ordered longest-code-first so parsing prefers the two-letter diagonals
        // over a single-letter prefix (e.g. "LD" isn't read as "L").
        private static readonly IReadOnlyList<(string Code, FacingDirection Direction)> Codes = new[]
        {
            ("LU", FacingDirection.LeftUp),
            ("LD", FacingDirection.LeftDown),
            ("RU", FacingDirection.RightUp),
            ("RD", FacingDirection.RightDown),
            ("L", FacingDirection.Left),
            ("R", FacingDirection.Right),
            ("U", FacingDirection.Up),
            ("D", FacingDirection.Down),
        };

        public static string ToCode(this FacingDirection direction) => direction switch
        {
            FacingDirection.Left => "L",
            FacingDirection.Right => "R",
            FacingDirection.Up => "U",
            FacingDirection.Down => "D",
            FacingDirection.LeftUp => "LU",
            FacingDirection.LeftDown => "LD",
            FacingDirection.RightUp => "RU",
            FacingDirection.RightDown => "RD",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
        };

        public static string Describe(this FacingDirection direction) => direction switch
        {
            FacingDirection.Left => "facing left",
            FacingDirection.Right => "facing right",
            FacingDirection.Up => "facing up",
            FacingDirection.Down => "facing down",
            FacingDirection.LeftUp => "facing up and to the left",
            FacingDirection.LeftDown => "facing down and to the left",
            FacingDirection.RightUp => "facing up and to the right",
            FacingDirection.RightDown => "facing down and to the right",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
        };

        /// <summary>
        /// Classifies a model response into a <see cref="FacingDirection"/>, a deliberate
        /// "unsure" answer, or an unrecognisable reply. The model is asked to reply with
        /// just a code, but this tolerates surrounding prose/punctuation by scanning the
        /// uppercased letters. The <see cref="UnsureCode"/> sentinel is checked first
        /// because the word "UNSURE" itself contains letters (U, R) that would otherwise
        /// match a direction code. <see cref="OrientationOutcome.Resolved"/> is the only
        /// outcome that yields a non-null direction.
        /// </summary>
        public static (FacingDirection? Direction, OrientationOutcome Outcome) Classify(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return (null, OrientationOutcome.Unrecognised);
            }

            var letters = new string(response.Where(char.IsLetter).ToArray()).ToUpperInvariant();

            if (letters.Contains(UnsureCode))
            {
                return (null, OrientationOutcome.Unsure);
            }

            foreach (var (code, direction) in Codes)
            {
                if (letters.Contains(code))
                {
                    return (direction, OrientationOutcome.Resolved);
                }
            }

            return (null, OrientationOutcome.Unrecognised);
        }
    }
}
