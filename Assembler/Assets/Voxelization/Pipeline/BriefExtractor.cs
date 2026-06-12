using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Stage 1a: transcribes a reference image into a <see cref="ReferenceBrief"/>
	/// in a dedicated vision call, separate from planning. When one call did
	/// both, the model harmonised its "transcription" with whatever it planned
	/// (a 9-wide silhouette to justify 3-wide arms) — and every downstream gate
	/// then validated the plan against its own hallucination. The extractor has
	/// no plan to defend, so the brief stays an honest read of the image.
	/// </summary>
	public sealed class BriefExtractor
	{
		public const string Stage = "1-brief";

		private readonly IAnthropicGateway _gateway;
		private readonly VoxelizationConfig _config;

		public BriefExtractor(IAnthropicGateway gateway, VoxelizationConfig config)
		{
			_gateway = gateway;
			_config = config;
		}

		public async Task<ReferenceBrief> ExtractAsync(
			SetManifest manifest,
			ManifestAsset asset,
			AnthropicImage image,
			CancellationToken ct)
		{
			if (image.IsEmpty)
			{
				return ReferenceBrief.None;
			}

			var messages = new List<AnthropicMessage>
			{
				new("user", VoxelizationPrompts.BriefUser(manifest, asset), new[] { image }),
			};

			for (var attempt = 1; ; attempt++)
			{
				var response = await _gateway.SendAsync(
					Stage, _config.PlanningModel, VoxelizationPrompts.BriefSystem, messages, ct).ConfigureAwait(false);

				try
				{
					var yaml = FencedBlockExtractor.Extract(response, "brief")
							   ?? throw new FormatException("Response contained no ```brief fenced block.");
					var brief = TrimSilhouette(ReferenceBriefYaml.Read(yaml) with { Source = asset.Reference });
					return asset.Symmetry == "bilateral" ? SymmetrizeSilhouette(brief) : brief;
				}
				catch (FormatException ex)
				{
					if (attempt >= 2)
					{
						throw new VoxelizationException(
							$"Extracting the reference brief for '{asset.Id}' failed: {ex.Message}", ex);
					}

					messages.Add(new AnthropicMessage("assistant", response));
					messages.Add(new AnthropicMessage("user",
						$"That brief could not be parsed: {ex.Message}\nEmit the corrected ```brief block."));
				}
			}
		}

		/// <summary>
		/// Drops fully-empty margin rows/columns and re-anchors the declared size
		/// to the trimmed grid. Vision reads often pad the subject with empty
		/// margin, which inflates the silhouette width and then poisons every
		/// width-anchored check downstream.
		/// </summary>
		private static ReferenceBrief TrimSilhouette(ReferenceBrief brief)
		{
			var silhouette = brief.Silhouette;
			if (silhouette.IsEmpty)
			{
				return brief;
			}

			static bool RowEmpty(string row) => row.All(c => !SilhouetteSpec.IsSolid(c));

			var rows = silhouette.Rows;
			var top = 0;
			var bottom = rows.Count - 1;
			while (top <= bottom && RowEmpty(rows[top]))
			{
				top++;
			}

			while (bottom >= top && RowEmpty(rows[bottom]))
			{
				bottom--;
			}

			if (top > bottom)
			{
				return brief;
			}

			var width = rows.Max(r => r.Length);
			var slice = rows.Skip(top).Take(bottom - top + 1).Select(r => r.PadRight(width, '.')).ToList();
			var left = slice.Min(r => FirstSolid(r));
			var right = slice.Max(r => LastSolid(r));
			var trimmed = slice.Select(r => r.Substring(left, right - left + 1)).ToArray();

			return brief with
			{
				Silhouette = silhouette with
				{
					Rows = trimmed,
					Size = new UnityEngine.Vector3Int(right - left + 1, trimmed.Length, 0),
				},
			};

			static int FirstSolid(string row)
			{
				for (var i = 0; i < row.Length; i++)
				{
					if (SilhouetteSpec.IsSolid(row[i]))
					{
						return i;
					}
				}

				return row.Length - 1;
			}

			static int LastSolid(string row)
			{
				for (var i = row.Length - 1; i >= 0; i--)
				{
					if (SilhouetteSpec.IsSolid(row[i]))
					{
						return i;
					}
				}

				return 0;
			}
		}

		/// <summary>
		/// A lopsided vision read of a bilateral subject would poison both the
		/// authoring guidance and the validation oracles, so the silhouette is
		/// forced symmetric in code: each row becomes the union of itself and its
		/// reflection.
		/// </summary>
		private static ReferenceBrief SymmetrizeSilhouette(ReferenceBrief brief)
		{
			if (brief.Silhouette.IsEmpty)
			{
				return brief;
			}

			var rows = brief.Silhouette.Rows
				.Select(row => new string(Enumerable.Range(0, row.Length)
					.Select(i => row[i] == '#' || row[row.Length - 1 - i] == '#' ? '#' : '.')
					.ToArray()))
				.ToArray();

			return brief with { Silhouette = brief.Silhouette with { Rows = rows } };
		}
	}
}
