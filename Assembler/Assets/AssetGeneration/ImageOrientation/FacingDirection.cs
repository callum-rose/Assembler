using System;
using System.Collections.Generic;
using System.Linq;

namespace Assembler.AssetGeneration.ImageOrientation
{
    /// <summary>
    /// The direction the front of an object faces. Eight of the codes lie within the
    /// 2D plane of the image (L, R, U, D and the four diagonals), relative to the image
    /// edges as seen by the viewer — so a car whose front points at the bottom-left
    /// corner is <see cref="LeftDown"/> (code "LD"). Two more codes cover the axis
    /// perpendicular to the image: <see cref="Towards"/> (front points out of the screen,
    /// toward the viewer; code "T") and <see cref="Away"/> (front points into the screen,
    /// away from the viewer; code "A").
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
        Towards,
        Away,
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
            ("T", FacingDirection.Towards),
            ("A", FacingDirection.Away),
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
            FacingDirection.Towards => "T",
            FacingDirection.Away => "A",
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
            FacingDirection.Towards => "facing toward the viewer (out of the screen)",
            FacingDirection.Away => "facing away from the viewer (into the screen)",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
        };

        /// <summary>
        /// Classifies a single-image model response into the <see cref="OrientationAnswer"/> union:
        /// a <see cref="OrientationAnswer.Facing"/> direction, a deliberate
        /// <see cref="OrientationAnswer.Unsure"/>, or an <see cref="OrientationAnswer.Unrecognised"/>
        /// reply. The model is asked to reply with just a code, but this tolerates surrounding
        /// prose/punctuation by scanning the uppercased letters. The <see cref="UnsureCode"/> sentinel
        /// is checked first because the word "UNSURE" itself contains letters (U, R) that would
        /// otherwise match a direction code.
        /// </summary>
        public static OrientationAnswer Classify(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return new OrientationAnswer.Unrecognised();
            }

            var letters = new string(response.Where(char.IsLetter).ToArray()).ToUpperInvariant();

            if (letters.Contains(UnsureCode))
            {
                return new OrientationAnswer.Unsure();
            }

            foreach (var (code, direction) in Codes)
            {
                if (letters.Contains(code))
                {
                    return new OrientationAnswer.Facing(direction);
                }
            }

            return new OrientationAnswer.Unrecognised();
        }
    }
}
