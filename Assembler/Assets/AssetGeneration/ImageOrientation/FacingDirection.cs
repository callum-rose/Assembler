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

    public static class FacingDirectionExtensions
    {
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
        /// Extracts a <see cref="FacingDirection"/> from a model response. The model
        /// is asked to reply with just the code, but this tolerates surrounding
        /// prose/punctuation by scanning the uppercased letters for the first valid
        /// code (diagonals first). Returns null when no recognisable code is present.
        /// </summary>
        public static FacingDirection? Parse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return null;
            }

            var letters = new string(response.Where(char.IsLetter).ToArray()).ToUpperInvariant();

            foreach (var (code, direction) in Codes)
            {
                if (letters.Contains(code))
                {
                    return direction;
                }
            }

            return null;
        }
    }
}
