using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxels.Scripting;

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
				return await new ManifestGenerator(gateway, config).GenerateAsync(options.Brief, ct).ConfigureAwait(false);
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
	/// Headless entry point for the voxelization pipeline, driven by
	/// <c>Tools/voxelize.sh</c>:
	///   Unity -batchmode -quit -nographics -projectPath &lt;project&gt;
	///         -executeMethod Editor.VoxelizeBatch.Run -logFile -
	///         [-brief &lt;text&gt; | -manifest &lt;path&gt;] [-imageFolder ..] [-outputFolder ..]
	///         [-only a,b] [-note ..] [-manifestModel ..] [-planningModel ..]
	///         [-authoringModel ..] [-concurrency N]
	///
	/// Constructs a <see cref="ClaudeCliGateway"/> (so the run bills the Claude plan,
	/// not API credits), runs <see cref="VoxelizeRunner"/>, refreshes the asset
	/// database, and exits non-zero if any asset failed. Because the export step
	/// touches Unity main-thread APIs (<c>Texture2D.EncodeToPNG</c>), the async run is
	/// driven by a single-thread message pump on the main thread rather than a
	/// blocking wait — continuations that produce the preview PNGs run on the editor
	/// thread, never the thread pool.
	/// </summary>
	public static class VoxelizeBatch
	{
		public static void Run()
		{
			SuppressLogStackTraces();
			try
			{
				var args = Environment.GetCommandLineArgs();
				var options = ParseOptions(args);
				var settings = VoxelizationSettings.LoadOrCreate();
				var config = ApplyModelOverrides(settings.ToConfig(), args);
				var concurrency = IntArg(args, "-concurrency", ClaudeCliGateway.DefaultConcurrency);

				var usage = new TokenUsageTracker();
				var log = new BatchProgress();

				VoxelizeRunResult result;
				using (var gateway = new ClaudeCliGateway(usage, concurrency))
				{
					result = AsyncPump.Run(() =>
						VoxelizeRunner.RunAsync(gateway, config, options, usage, log, CancellationToken.None));
				}

				AssetDatabase.Refresh();

				var report = BuildReport(result, usage, config);
				var ok = result.Results.Count > 0 && result.Results.All(r => r.Status != ModelStatus.Failed);
				if (ok)
				{
					Debug.Log(report);
				}
				else
				{
					Debug.LogError(report);
				}

				EditorApplication.Exit(ok ? 0 : 1);
			}
			catch (Exception e)
			{
				Debug.LogError("VoxelizeBatch failed: " + e);
				EditorApplication.Exit(1);
			}
		}

		private static VoxelizeOptions ParseOptions(string[] args) => new()
		{
			Brief = ArgValue(args, "-brief"),
			ManifestPath = ArgValue(args, "-manifest"),
			ImageFolder = ArgValue(args, "-imageFolder"),
			OutputFolder = ArgValue(args, "-outputFolder", "Assets/GeneratedVoxels"),
			Note = ArgValue(args, "-note"),
			Only = ArgValue(args, "-only")
				.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(s => s.Trim())
				.Where(s => s.Length > 0)
				.ToList(),
		};

		private static VoxelizationConfig ApplyModelOverrides(VoxelizationConfig config, string[] args)
		{
			var manifestModel = ArgValue(args, "-manifestModel");
			var planningModel = ArgValue(args, "-planningModel");
			var authoringModel = ArgValue(args, "-authoringModel");
			return config with
			{
				ManifestModel = manifestModel.Length > 0 ? manifestModel : config.ManifestModel,
				PlanningModel = planningModel.Length > 0 ? planningModel : config.PlanningModel,
				AuthoringModel = authoringModel.Length > 0 ? authoringModel : config.AuthoringModel,
			};
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

		private static string ArgValue(string[] args, string flag, string fallback = "")
		{
			for (var i = 0; i < args.Length - 1; i++)
			{
				if (args[i] == flag)
				{
					return args[i + 1];
				}
			}

			return fallback;
		}

		private static int IntArg(string[] args, string flag, int fallback)
		{
			var raw = ArgValue(args, flag);
			return int.TryParse(raw, out var value) ? value : fallback;
		}

		private static void SuppressLogStackTraces()
		{
			// Batch-only, process-wide: keep the report block from being trailed by a
			// script stack trace (which reads like a failure). Mirrors EditorBatchCli.
			Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
			Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
		}

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

		/// <summary>
		/// Minimal single-thread message pump (Stephen Toub's AsyncPump): installs a
		/// synchronization context whose posted continuations are drained on the
		/// calling (editor main) thread until the task completes. This is what lets the
		/// pipeline's main-thread-only export run on the editor thread in batch mode,
		/// where Unity's own update loop isn't pumping a blocked <c>-executeMethod</c>.
		/// </summary>
		private static class AsyncPump
		{
			public static T Run<T>(Func<Task<T>> func)
			{
				var previous = SynchronizationContext.Current;
				var context = new SingleThreadSynchronizationContext();
				SynchronizationContext.SetSynchronizationContext(context);
				try
				{
					var task = func();
					task.ContinueWith(_ => context.Complete(), TaskScheduler.Default);
					context.RunOnCurrentThread();
					return task.GetAwaiter().GetResult();
				}
				finally
				{
					SynchronizationContext.SetSynchronizationContext(previous);
				}
			}

			private sealed class SingleThreadSynchronizationContext : SynchronizationContext
			{
				private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

				public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

				public override void Send(SendOrPostCallback d, object? state) => d(state);

				public void Complete() => _queue.CompleteAdding();

				public void RunOnCurrentThread()
				{
					foreach (var work in _queue.GetConsumingEnumerable())
					{
						work.Callback(work.State);
					}
				}
			}
		}
	}
}
