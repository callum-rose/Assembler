using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxelization
{
	public sealed record ModelPlan(VoxelRigModel Skeleton, ReferenceBrief Brief);

	/// <summary>
	/// Stage 1b: one call per model producing the part skeleton. The reference
	/// brief arrives as locked INPUT (independently transcribed by
	/// <see cref="BriefExtractor"/>), so the deterministic gates judge the plan
	/// against ground truth the planner cannot bend to fit its own design. Code
	/// re-anchors whatever came back to the manifest's bounding box and demotes
	/// over-budget layers parts to scripts.
	/// </summary>
	public sealed class ModelPlanner
	{
		public const string Stage = "1-planning";

		/// <summary>
		/// Total planning calls per asset: the first plan plus feedback rounds for
		/// parse/geometry failures. Tight-budget assets (a standing quadruped fitting
		/// feet+legs+torso+head into a short height) need several correction rounds to
		/// converge on the bounding box, so this is deliberately generous.
		/// </summary>
		public const int MaxAttempts = 5;

		private readonly IAnthropicGateway _gateway;
		private readonly VoxelizationConfig _config;

		public ModelPlanner(IAnthropicGateway gateway, VoxelizationConfig config)
		{
			_gateway = gateway;
			_config = config;
		}

		public async Task<ModelPlan> PlanAsync(
			SetManifest manifest,
			ManifestAsset asset,
			IReadOnlyList<AnthropicImage> referenceImages,
			ReferenceBrief brief,
			string refinementNote,
			CancellationToken ct,
			string previousModelYaml = "",
			bool suppressPaletteGate = false)
		{
			// All of the asset's labelled images go to the multimodal message for
			// styling detail the brief doesn't capture.
			var attachments = referenceImages.Where(i => !i.IsEmpty).ToArray();
			var hasImage = attachments.Length > 0;
			var userText = VoxelizationPrompts.PlanningUser(
				manifest, asset, brief, hasImage, refinementNote, _config.StyleGuidance, previousModelYaml);
			var messages = new List<AnthropicMessage>
			{
				new("user", userText, attachments),
			};

			for (var attempt = 1; ; attempt++)
			{
				var response = await _gateway.SendAsync(
					Stage, _config.PlanningModel, VoxelizationPrompts.PlanningSystem(_config), messages, ct).ConfigureAwait(false);

				var (plan, feedback) = TryParse(response, manifest, asset, brief, suppressPaletteGate);
				if (plan != null)
				{
					return plan;
				}

				if (attempt >= MaxAttempts)
				{
					throw new VoxelizationException($"Planning '{asset.Id}' failed after {MaxAttempts} attempts: {feedback}");
				}

				messages.Add(new AnthropicMessage("assistant", response));
				messages.Add(new AnthropicMessage("user", feedback));
			}
		}

		private (ModelPlan? Plan, string Feedback) TryParse(
			string response, SetManifest manifest, ManifestAsset asset, ReferenceBrief brief, bool suppressPaletteGate)
		{
			ModelPlan plan;
			try
			{
				plan = Parse(response, manifest, asset, brief);
			}
			catch (FormatException ex)
			{
				return (null, $"That plan could not be parsed: {ex.Message}\nEmit the corrected ```vmodel block.");
			}

			// The plan is a SKELETON: parts must be `planned` (or free mirrors/copies),
			// never authored inline. A planner that writes geometry itself skips the
			// authoring stage entirely and the model assembles to nothing — so reject
			// it here and make the planner emit `planned` placeholders.
			var inlineAuthored = plan.Skeleton.Parts
				.Where(p => p.Data is LayersPartData or ScriptPartData or PrimitivesPartData)
				.Select(p => $"{p.Id} ({p.Data.Encoding.ToString().ToLowerInvariant()})")
				.ToList();
			if (inlineAuthored.Count > 0)
			{
				return (null,
					"The plan must be a SKELETON only — these parts carry inline geometry instead of being planned: " +
					string.Join(", ", inlineAuthored) + ". Declare EACH authored part as " +
					"`data: { encoding: planned, planned: layers|script|primitives, size: [x,y,z], offset: [x,y,z], note: \"...\" }` " +
					"and write NO geometry yourself (no layers, no shapes, no script) — every part is authored in a separate later " +
					"stage.\nEmit the corrected ```vmodel block.");
			}

			var geometryErrors = PlanGeometryChecks.Errors(plan.Skeleton);
			if (geometryErrors.Count > 0)
			{
				return (null,
					"That skeleton can never assemble bilaterally symmetric — these were rejected by deterministic geometry checks:\n- " +
					string.Join("\n- ", geometryErrors) +
					"\nFix the skeleton and emit the corrected ```vmodel block.");
			}

			// A noted refine lets the operator introduce colours the locked brief
			// palette never had ("make the car red") — the note outranks the brief.
			if (brief.Palette.Count > 0 && !suppressPaletteGate)
			{
				var allowed = new HashSet<int>(brief.Palette.Select(ColourKey));
				var rogue = plan.Skeleton.Palette
					.Where(e => !allowed.Contains(ColourKey(e)))
					.Select(e => e.ToHex())
					.ToList();
				if (rogue.Count > 0)
				{
					return (null,
						$"The plan's palette contains colours not in the locked reference palette: {string.Join(", ", rogue)}. " +
						$"Use ONLY the brief's colours, hex-exact: {string.Join(", ", brief.Palette.Select(e => e.ToHex()))}.\n" +
						"Emit the corrected ```vmodel block.");
				}
			}

			var feasibility = PlanGeometryChecks.SilhouetteFeasibilityError(
				plan.Skeleton, brief, _config.SilhouetteCoverageThreshold);
			return feasibility == null
				? (plan, string.Empty)
				: (null, feasibility + "\nEmit the corrected ```vmodel block.");
		}

		private static int ColourKey(PaletteEntry entry) =>
			(entry.Colour.r << 16) | (entry.Colour.g << 8) | entry.Colour.b;

		private ModelPlan Parse(string response, SetManifest manifest, ManifestAsset asset, ReferenceBrief brief)
		{
			var vmodelYaml = FencedBlockExtractor.Extract(response, "vmodel")
							 ?? throw new FormatException("Response contained no ```vmodel fenced block.");
			var skeleton = VModelYaml.Read(vmodelYaml);
			if (skeleton.Parts.Count == 0)
			{
				throw new FormatException("The plan declared no parts.");
			}

			if (skeleton.Palette.Count == 0)
			{
				throw new FormatException("The plan declared no palette.");
			}

			// The manifest, not the plan, owns identity, scale, and symmetry.
			skeleton = skeleton with
			{
				Id = asset.Id,
				Description = asset.Description,
				TargetHeight = asset.Height,
				TargetLength = asset.Length,
				TargetWidth = asset.Width,
				SizeTolerance = asset.Tolerance,
				Rigged = asset.Rig,
				Symmetry = asset.Symmetry,
				Parts = skeleton.Parts.Select(EnforceBudget).ToArray(),
			};

			return new ModelPlan(skeleton, brief);
		}

		private VoxelPart EnforceBudget(VoxelPart part)
		{
			if (part.Data is not PlannedPartData planned || planned.PlannedEncoding != PartEncoding.Layers)
			{
				return part;
			}

			var volume = planned.Size.x * planned.Size.y * planned.Size.z;
			return volume <= _config.PartVoxelBudget
				? part
				: part with
				{
					Data = planned with
					{
						PlannedEncoding = PartEncoding.Script,
						Note = planned.Note.Length > 0
							? planned.Note + " (demoted to script: declared volume exceeds the layers budget)"
							: "demoted to script: declared volume exceeds the layers budget",
					},
				};
		}
	}
}
