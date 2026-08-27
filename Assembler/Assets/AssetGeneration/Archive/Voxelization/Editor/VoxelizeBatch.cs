using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxels.Scripting;
using Unity.Pipeline.Commands;

namespace Assembler.Voxelization.Editor
{
	/// <summary>
	/// Parsed inputs for one headless voxelization run. Either <see cref="Brief"/>
	/// (generate a manifest first) or <see cref="ManifestPath"/> (run an existing
	/// one) must be set. The rest mirror the editor window's knobs.
	/// </summary>
	public sealed record VoxelizeOptions
	{
		public string Brief { get; init; } = string.Empty;
		public string ManifestPath { get; init; } = string.Empty;
		public string ImageFolder { get; init; } = string.Empty;
		public string OutputFolder { get; init; } = "Assets/GeneratedVoxels";

		/// <summary>When non-empty, only these asset ids from the manifest are run.</summary>
		public IReadOnlyList<string> Only { get; init; } = Array.Empty<string>();

		/// <summary>A note/refinement instruction threaded into each asset run (suppresses the palette gate, like the editor's refine path).</summary>
		public string Note { get; init; } = string.Empty;
	}

	public sealed record VoxelizeRunResult(string RunFolder, IReadOnlyList<ModelResult> Results, IReadOnlyList<StageUsage> Usage);

	/// <summary>
	/// Host-agnostic core of a voxelization run: manifest (read or generate) →
	/// orchestrate every asset → write each export to disk. Depends only on the
	/// pipeline assemblies, UnityEngine math/texture types and file IO — no
	/// <c>UnityEditor</c> — so it can be lifted into a console host later. The
	/// editor-only glue (settings asset, <c>AssetDatabase.Refresh</c>, command-line
	/// parsing, the batch-mode main-thread pump) lives in <see cref="Editor.VoxelizeBatch"/>.
	/// </summary>
	public static class VoxelizeRunner
	{
		public static async Task<VoxelizeRunResult> RunAsync(
			IAnthropicGateway gateway,
			VoxelizationConfig config,
			VoxelizeOptions options,
			TokenUsageTracker usage,
			IProgress<string> log,
			CancellationToken ct)
		{
			var manifest = await ResolveManifestAsync(gateway, config, options, log, ct).ConfigureAwait(false);
			manifest = FilterToOnly(manifest, options.Only);

			var images = string.IsNullOrWhiteSpace(options.ImageFolder)
				? (IReferenceImageSource)NullReferenceImageSource.Instance
				: new FileReferenceImageSource(options.ImageFolder);

			var missing = await SetOrchestrator.MissingReferencesAsync(manifest, images, ct).ConfigureAwait(false);
			if (missing.Count > 0)
			{
				log.Report("WARNING: these reference files are missing and will fail their assets:\n  " +
						   string.Join("\n  ", missing));
			}

			var runFolder = await ResolveRunFolderAsync(gateway, config, manifest, options.OutputFolder, log, ct).ConfigureAwait(false);

			var scriptRunner = new ExecutorPartScriptRunner(new VoxelScriptExecutor(config.ScriptLimits));
			var orchestrator = new SetOrchestrator(gateway, config, images, scriptRunner, usage);

			// Assets run concurrently exactly as the editor window does; the real
			// process cap is enforced inside the gateway's semaphore, so this never
			// spawns more than --concurrency claude processes regardless of asset count.
			var results = await Task.WhenAll(manifest.Assets.Select(async asset =>
			{
				var result = await orchestrator.RunAssetAsync(manifest, asset, options.Note, ct, log).ConfigureAwait(false);
				ExportToDisk(result, runFolder, log);
				return result;
			})).ConfigureAwait(false);

			return new VoxelizeRunResult(runFolder, results, usage.Snapshot());
		}

		private static async Task<SetManifest> ResolveManifestAsync(
			IAnthropicGateway gateway, VoxelizationConfig config, VoxelizeOptions options, IProgress<string> log, CancellationToken ct)
		{
			if (!string.IsNullOrWhiteSpace(options.ManifestPath))
			{
				log.Report($"Reading manifest: {options.ManifestPath}");
				if (!File.Exists(options.ManifestPath))
				{
					throw new VoxelizationException($"Manifest file not found: {options.ManifestPath}");
				}

				return ManifestYaml.Read(File.ReadAllText(options.ManifestPath));
			}

			if (!string.IsNullOrWhiteSpace(options.Brief))
			{
				log.Report("Generating manifest from brief...");
				return await new ManifestGenerator(gateway, config).GenerateAsync(options.Brief, ct, log).ConfigureAwait(false);
			}

			throw new VoxelizationException("Nothing to run: pass either a brief (--brief) or a manifest path (--manifest).");
		}

		private static SetManifest FilterToOnly(SetManifest manifest, IReadOnlyList<string> only)
		{
			if (only.Count == 0)
			{
				return manifest;
			}

			var wanted = new HashSet<string>(only, StringComparer.OrdinalIgnoreCase);
			var assets = manifest.Assets.Where(a => wanted.Contains(a.Id)).ToList();
			if (assets.Count == 0)
			{
				throw new VoxelizationException($"--only matched no manifest assets (asked for: {string.Join(", ", only)}).");
			}

			return manifest with { Assets = assets };
		}

		/// <summary>
		/// Mirrors the window's run-folder naming: "{timestamp}-{slug}" with the slug
		/// generated from the manifest, falling back to a plain timestamp folder if the
		/// naming call fails. Cancellation propagates.
		/// </summary>
		private static async Task<string> ResolveRunFolderAsync(
			IAnthropicGateway gateway, VoxelizationConfig config, SetManifest manifest, string outputFolder, IProgress<string> log, CancellationToken ct)
		{
			var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
			try
			{
				log.Report("Naming run folder...");
				var slug = await new RunFolderNamer(gateway, config).NameAsync(manifest, ct).ConfigureAwait(false);
				var folder = Path.Combine(outputFolder, slug.Length > 0 ? $"{timestamp}-{slug}" : $"run-{timestamp}");
				log.Report($"Run folder: {folder}");
				return folder;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				log.Report("Run folder naming failed; keeping timestamp: " + ex.Message);
				return Path.Combine(outputFolder, $"run-{timestamp}");
			}
		}

		private static void ExportToDisk(ModelResult result, string runFolder, IProgress<string> log)
		{
			log.Report($"{result.AssetId}: {result.Status}");
			if (result.Status == ModelStatus.Failed)
			{
				log.Report($"{result.AssetId}: FAILED — {result.Error}");
				return;
			}

			if (result.Export == null)
			{
				return;
			}

			var directory = Path.Combine(runFolder, result.AssetId);
			result.Export.WriteToDisk(directory);
			log.Report($"{result.AssetId}: exported -> {directory}");
		}
	}
}

namespace Editor
{
	using Assembler.Voxelization;
	using Assembler.Voxelization.Editor;
	using UnityEditor;
	using UnityEngine;

	/// <summary>
	/// CLI entry point for the voxelization pipeline, reached as <c>unity command voxelize</c>.
	///
	/// Constructs a <see cref="ClaudeCliGateway"/> (so the run bills the Claude plan,
	/// not API credits), runs <see cref="VoxelizeRunner"/>, refreshes the asset
	/// database, and fails the command if any asset failed. The export step touches
	/// Unity main-thread APIs (<c>Texture2D.EncodeToPNG</c>); awaiting inside a command
	/// resumes on the editor's own synchronization context, so those continuations run
	/// on the editor thread, never the thread pool.
	/// </summary>
	public static class VoxelizeBatch
	{
		/// <summary>
		/// Pipeline entry point, reached as <c>unity command voxelize</c>. Same run as <see cref="Run"/>,
		/// driven from a resident editor instead of a batch boot.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Declared <c>async Task</c> deliberately. The Pipeline server dispatches the synchronous part of
		/// a main-thread command under a 60s budget, then awaits the returned Task on a background thread
		/// while the editor keeps pumping — so an async command is not bound by that budget, and a run
		/// lasting many minutes is fine. Awaiting here resumes on Unity's own editor-thread
		/// synchronization context, so the main-thread-only export steps still land on the main thread.
		/// (The <c>-executeMethod</c> entry point this replaced needed a hand-rolled message pump for
		/// that, because nothing pumps a blocked batch-mode call.)
		/// </para>
		/// <para>
		/// The CLI's own <c>--timeout</c> (30s by default) is a client-side wait and will give up long
		/// before a real run finishes, even though the run continues server-side. Pass a generous
		/// <c>--timeout</c>, or <c>--detach</c> and poll with <c>unity job</c>.
		/// </para>
		/// </remarks>
		[CliCommand("voxelize", "Generate voxel models from a brief or a manifest via the LLM-driven "
			+ "voxelization pipeline. Long-running — use --detach or a large --timeout.",
			Tags = new[] { "assembler/assetgen" })]
		public static async Task<string> VoxelizeCommand(
			[CliArg("brief", "Natural-language brief to generate a manifest from, "
				+ "e.g. 'pirate cove props'. Mutually exclusive with --manifest.")]
			string? brief = null,
			[CliArg("manifest", "Path to an existing .manifest.yaml to run. Mutually exclusive with --brief.")]
			string? manifest = null,
			[CliArg("image-folder", "Folder of reference images to condition generation on.")]
			string? imageFolder = null,
			[CliArg("output-folder", "Where generated voxel assets are written.")]
			string outputFolder = "Assets/GeneratedVoxels",
			[CliArg("only", "Comma-separated asset ids to restrict the run to, e.g. 'tree,rock'.")]
			string? only = null,
			[CliArg("note", "Extra instruction applied to the whole run, e.g. 'make them chunkier'.")]
			string? note = null,
			[CliArg("manifest-model", "Override the model used for the manifest stage.")]
			string? manifestModel = null,
			[CliArg("planning-model", "Override the model used for the planning stage.")]
			string? planningModel = null,
			[CliArg("authoring-model", "Override the model used for the authoring stage.")]
			string? authoringModel = null,
			[CliArg("concurrency", "How many assets to generate at once.")]
			int concurrency = 0)
		{
			if (string.IsNullOrWhiteSpace(brief) == string.IsNullOrWhiteSpace(manifest))
			{
				throw new ArgumentException("pass exactly one of --brief or --manifest.");
			}

			var options = new VoxelizeOptions
			{
				Brief = brief ?? string.Empty,
				ManifestPath = manifest ?? string.Empty,
				ImageFolder = imageFolder ?? string.Empty,
				OutputFolder = outputFolder,
				Note = note ?? string.Empty,
				Only = (only ?? string.Empty)
					.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(x => x.Trim())
					.Where(x => x.Length > 0)
					.ToList(),
			};

			var settings = VoxelizationSettings.LoadOrCreate();
			var config = settings.ToConfig() with
			{
				ManifestModel = Override(manifestModel, settings.ToConfig().ManifestModel),
				PlanningModel = Override(planningModel, settings.ToConfig().PlanningModel),
				AuthoringModel = Override(authoringModel, settings.ToConfig().AuthoringModel),
			};

			var usage = new TokenUsageTracker();
			var log = new BatchProgress();

			VoxelizeRunResult result;
			using (var gateway = new ClaudeCliGateway(
				usage, concurrency > 0 ? concurrency : ClaudeCliGateway.DefaultConcurrency))
			{
				result = await VoxelizeRunner.RunAsync(
					gateway, config, options, usage, log, CancellationToken.None);
			}

			AssetDatabase.Refresh();

			var report = BuildReport(result, usage, config);
			var ok = result.Results.Count > 0 && result.Results.All(r => r.Status != ModelStatus.Failed);
			if (!ok)
			{
				// Surface as a command failure so the CLI exits non-zero, matching the batch path's exit code.
				throw new InvalidOperationException(report);
			}

			return report;
		}

		private static string Override(string? value, string fallback) =>
			string.IsNullOrWhiteSpace(value) ? fallback : value!;

		/// <summary>Logs each pipeline progress line straight to the Unity log as it arrives.</summary>
		private sealed class BatchProgress : IProgress<string>
		{
			private readonly object _gate = new();

			public void Report(string value)
			{
				lock (_gate)
				{
					Debug.Log(value);
				}
			}
		}

		private static string BuildReport(VoxelizeRunResult result, TokenUsageTracker usage, VoxelizationConfig config)
		{
			var sb = new StringBuilder();
			sb.AppendLine("============== Voxelization run ==============");
			sb.AppendLine("Run folder: " + result.RunFolder);
			foreach (var model in result.Results)
			{
				sb.Append(model.Status switch
				{
					ModelStatus.Ready => "OK         ",
					ModelStatus.NeedsReview => "REVIEW     ",
					_ => "FAIL       ",
				});
				sb.Append(model.AssetId);
				if (model.Error.Length > 0)
				{
					sb.Append("  — ").Append(model.Error);
				}

				sb.AppendLine();
			}

			// API-equivalent spend the plan billing avoided, via the existing pricing model.
			var totalUsd = usage.Snapshot()
				.Sum(stage => TokenPricing.EstimateUsd(stage.Tokens, TokenPricing.RatesFor(config.ModelForStage(stage.Stage))));
			sb.AppendLine();
			var failed = result.Results.Count(r => r.Status == ModelStatus.Failed);
			sb.AppendLine(failed == 0
				? $"All {result.Results.Count} asset(s) produced a model."
				: $"{failed} of {result.Results.Count} asset(s) failed.");
			sb.AppendLine($"API-equivalent cost saved (billed to plan instead): ~${totalUsd:0.000}");
			sb.AppendLine("=============================================");
			return sb.ToString();
		}

	}
}
