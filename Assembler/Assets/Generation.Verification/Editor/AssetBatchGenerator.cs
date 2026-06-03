using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxels.Editor.Pipeline;
using UnityEditor;

namespace Assembler.Generation.Verification.Editor
{
	/// <summary>
	/// Generates every still-missing asset in a manifest concurrently, then does ONE
	/// main-thread import + load-probe pass. Each generation is isolated (its own
	/// client/executor) and bounded by a semaphore to avoid Anthropic 429s; a failed
	/// asset never fails the batch — it becomes a failed <see cref="AssetResult"/>.
	/// All <c>AssetDatabase</c>/<c>Resources</c> access is marshalled to the main thread.
	/// </summary>
	public sealed class AssetBatchGenerator
	{
		public async Task<IReadOnlyList<AssetResult>> GenerateMissingAsync(
			AssetGeneratorRegistry registry,
			IReadOnlyList<AssetRequest> requests,
			string apiKey,
			AssetGenerationOptions opts,
			int maxConcurrency,
			IGeneratorLogger? logger,
			CancellationToken ct)
		{
			var count = requests.Count;
			var results = new AssetResult?[count];
			if (count == 0) return Array.Empty<AssetResult>();

			// Resolve generators; unknown types fail immediately.
			var generators = new IAssetGenerator?[count];
			for (var i = 0; i < count; i++)
			{
				generators[i] = registry.Get(requests[i].Type);
				if (generators[i] == null)
				{
					results[i] = new AssetResult(requests[i], false,
						$"No generator registered for asset type '{requests[i].Type}'.");
				}
			}

			var dispatcher = EditorMainThreadDispatcher.Instance;

			// Main-thread pass: skip anything already loadable on disk so the fix-loop only
			// generates newly-referenced assets.
			var alreadyLoadable = new bool[count];
			await dispatcher.RunAsync(() =>
			{
				for (var i = 0; i < count; i++)
				{
					if (generators[i] == null) continue;
					try { alreadyLoadable[i] = generators[i]!.TryLoadGenerated(requests[i]); }
					catch { alreadyLoadable[i] = false; }
				}
			});

			for (var i = 0; i < count; i++)
			{
				if (results[i] == null && generators[i] != null && alreadyLoadable[i])
				{
					results[i] = new AssetResult(requests[i], true, null);
					logger?.Log($"Asset '{requests[i].Id}' ({requests[i].Type}) already present — skipping.");
				}
			}

			// Generate the remainder concurrently, bounded by the semaphore.
			var writtenPaths = new string?[count];
			var genErrors = new string?[count];
			using var sem = new SemaphoreSlim(Math.Max(1, maxConcurrency));
			var tasks = new List<Task>();
			for (var i = 0; i < count; i++)
			{
				if (results[i] != null) continue; // unknown type or already present
				var idx = i;
				tasks.Add(GenerateOneAsync(generators[idx]!, requests[idx], apiKey, opts, logger,
					sem, writtenPaths, genErrors, idx, ct));
			}

			await Task.WhenAll(tasks);
			ct.ThrowIfCancellationRequested();

			// Single main-thread pass: refresh, force-import each written file, then probe.
			await dispatcher.RunAsync(() =>
			{
				AssetDatabase.Refresh();

				for (var i = 0; i < count; i++)
				{
					var path = writtenPaths[i];
					if (string.IsNullOrEmpty(path)) continue;
					try
					{
						AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
					}
					catch (Exception ex)
					{
						genErrors[i] ??= "import failed: " + ex.Message;
					}
				}

				for (var i = 0; i < count; i++)
				{
					if (results[i] != null) continue;

					if (genErrors[i] != null)
					{
						results[i] = new AssetResult(requests[i], false, genErrors[i]);
						continue;
					}

					bool ok;
					try { ok = generators[i]!.TryLoadGenerated(requests[i]); }
					catch (Exception ex)
					{
						results[i] = new AssetResult(requests[i], false, "load probe threw: " + ex.Message);
						continue;
					}

					results[i] = ok
						? new AssetResult(requests[i], true, null)
						: new AssetResult(requests[i], false, "asset written but not loadable after import");
				}
			});

			var final = new AssetResult[count];
			for (var i = 0; i < count; i++)
			{
				final[i] = results[i] ?? new AssetResult(requests[i], false, "asset was not processed");
			}

			return final;
		}

		private static async Task GenerateOneAsync(
			IAssetGenerator generator, AssetRequest req, string apiKey, AssetGenerationOptions opts,
			IGeneratorLogger? logger, SemaphoreSlim sem, string?[] writtenPaths, string?[] genErrors,
			int idx, CancellationToken ct)
		{
			await sem.WaitAsync(ct);
			try
			{
				writtenPaths[idx] = await generator.GenerateAsync(req, apiKey, opts, logger, ct);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				genErrors[idx] = ex.Message;
				logger?.Log($"Asset '{req.Id}' ({req.Type}) generation failed: {ex.Message}");
			}
			finally
			{
				sem.Release();
			}
		}
	}
}
