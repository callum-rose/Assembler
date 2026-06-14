using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// An orthographic occupancy mask used as the automated validation oracle.
	/// <see cref="Face"/> names the projection (front = looking along -z,
	/// side = looking along -x, top = looking down -y). Rows are listed
	/// image-style, top row first; '#' is solid, '.' is empty.
	/// </summary>
	public sealed record SilhouetteSpec(string Face, Vector3Int Size, IReadOnlyList<string> Rows)
	{
		public static SilhouetteSpec None { get; } = new(string.Empty, Vector3Int.zero, Array.Empty<string>());

		public bool IsEmpty => Rows.Count == 0;

		/// <summary>
		/// Vision models sometimes transcribe silhouette rows with palette keys
		/// instead of '#'. Occupancy treats any non-empty marker as solid so a
		/// colour-keyed transcription cannot silently zero out every check.
		/// </summary>
		public static bool IsSolid(char cell) => cell is not ('.' or ' ' or '_');

		/// <summary>
		/// Renders the occupancy as colour-square emojis (🟩 solid, ⬜ empty), one
		/// glyph per cell, so the expected shape is legible at a glance in the run
		/// log — the '#'/'.' rows are hard to read as a shape when sizes get large.
		/// </summary>
		public string ToEmoji() =>
			string.Join("\n", Rows.Select(row => string.Concat(row.Select(c => IsSolid(c) ? "🟩" : "⬜"))));
	}

	/// <summary>
	/// Structured output of the single vision call over an asset's labelled
	/// reference images (Stage 1). When present it is authoritative: the palette
	/// is locked downstream, and proportions/silhouettes drive validation. There
	/// is one shared palette and one silhouette per (co-axially deduped) face.
	/// </summary>
	public sealed record ReferenceBrief
	{
		public string Source { get; init; } = string.Empty;
		public IReadOnlyList<PaletteEntry> Palette { get; init; } = Array.Empty<PaletteEntry>();
		public IReadOnlyDictionary<string, float> Proportions { get; init; } = new Dictionary<string, float>();
		public IReadOnlyList<string> SignatureFeatures { get; init; } = Array.Empty<string>();
		public IReadOnlyList<SilhouetteSpec> Silhouettes { get; init; } = Array.Empty<SilhouetteSpec>();

		public static ReferenceBrief None { get; } = new();

		/// <summary>
		/// One representative silhouette (front-preferred) for the few spots that
		/// want a single view, e.g. ASCII header text. Never null — falls back to
		/// <see cref="SilhouetteSpec.None"/>.
		/// </summary>
		public SilhouetteSpec PrimarySilhouette =>
			Silhouettes.FirstOrDefault(s => s.Face == "front")
			?? Silhouettes.FirstOrDefault(s => !s.IsEmpty)
			?? SilhouetteSpec.None;

		public bool IsEmpty => Source.Length == 0 && Palette.Count == 0 && Silhouettes.Count == 0;
	}
}
