using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Generation.Verification.Editor
{
	/// <summary>Bundles the descriptor outcome with the per-asset generation results.</summary>
	public sealed record GameWithAssetsResult(
		GenerationResult Generation,
		IReadOnlyList<AssetResult> Assets);

	/// <summary>
	/// Generate→assets→build loop. Mirrors <c>GenerationOrchestrator</c> but injects an
	/// asset-generation step: each attempt parses the manifest Claude emitted, generates
	/// the missing assets, imports them, then builds/verifies the descriptor that already
	/// references them. Failed-asset notes are fed into the next fix pass so Claude can
	/// adjust. Generating-missing per attempt keeps descriptor + assets coherent even if a
	/// fix introduces a new asset reference.
	///
	/// The orchestrator is stateful for the lifetime of one game: <see cref="GenerateAsync"/>
	/// starts a fresh game, and <see cref="ReviseAsync"/> continues the same conversation to
	/// revise it (reusing the same slug + descriptor file).
	/// </summary>
	public sealed class GameWithAssetsOrchestrator
	{
		private static readonly Regex TitleRegex = new(
			@"(?im)^\s*title\s*:\s*(?<value>.+?)\s*$", RegexOptions.Compiled);

		private const int MaxSlugLength = 48;

		private readonly string _apiKey;
		private readonly IGeneratorLogger _logger;
		private readonly AssetGeneratorRegistry _registry;
		private readonly AssetGenerationOptions _options;
		private readonly int _maxConcurrency;
		private readonly GameDescriptorGenerator _generator;
		private readonly AssetBatchGenerator _batch = new();

		private string _gameSlug = string.Empty;
		private string? _storedPath;

		public GameWithAssetsOrchestrator(string apiKey, IGeneratorLogger logger,
			AssetGenerationOptions? options = null, int maxConcurrency = 4,
			MeshSource meshSource = MeshSource.Script)
		{
			_apiKey = apiKey;
			_logger = logger;
			_registry = AssetGeneratorRegistry.For(meshSource);
			_options = options ?? AssetGenerationOptions.Default;
			_maxConcurrency = Math.Max(1, maxConcurrency);
			_generator = new GameDescriptorGenerator(new AnthropicClient(_apiKey));
		}

		/// <summary>True once a descriptor has been produced and can be revised.</summary>
		public bool CanRevise => _storedPath != null;

		/// <summary>Start a fresh game from a user prompt.</summary>
		public Task<GameWithAssetsResult> GenerateAsync(
			string userPrompt, int maxAttempts, CancellationToken ct)
		{
			_gameSlug = BuildSlug(userPrompt);
			_storedPath = null;
			var augmented = AssetAugmentedPrompt.Build(userPrompt, _gameSlug, _registry.SupportedTypes);
			return RunAsync(c => _generator.RequestInitialAsync(augmented, c), maxAttempts, ct);
		}

		/// <summary>Revise the previously generated game with a follow-up instruction.</summary>
		public Task<GameWithAssetsResult> ReviseAsync(
			string instruction, int maxAttempts, CancellationToken ct)
		{
			if (_storedPath == null)
			{
				throw new InvalidOperationException("Nothing to revise — generate a game first.");
			}

			var revision = AssetAugmentedPrompt.BuildRevision(instruction, _gameSlug, _registry.SupportedTypes);
			return RunAsync(c => _generator.RequestReviseAsync(revision, c), maxAttempts, ct);
		}

		private async Task<GameWithAssetsResult> RunAsync(
			Func<CancellationToken, Task<GeneratorResponse>> firstRequest, int maxAttempts, CancellationToken ct)
		{
			if (maxAttempts < 1)
			{
				maxAttempts = 1;
			}

			var attempts = new List<Attempt>();
			var assetResultsByKey = new Dictionary<string, AssetResult>();
			GeneratorResponse? lastResponse = null;
			IReadOnlyList<string> lastErrors = Array.Empty<string>();

			for (var i = 1; i <= maxAttempts; i++)
			{
				ct.ThrowIfCancellationRequested();

				GeneratorResponse response;
				try
				{
					Log($"Attempt {i}: requesting descriptor from Claude...");
					response = i == 1
						? await firstRequest(ct)
						: await _generator.RequestFixAsync(lastResponse!.Yaml ?? string.Empty, lastErrors, ct);
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					attempts.Add(new RequestFailedAttempt(i, "Anthropic request failed: " + ex.Message));
					Log($"Attempt {i}: request failed — {ex.Message}");
					break;
				}

				lastResponse = response;

				if (string.IsNullOrWhiteSpace(response.Yaml))
				{
					attempts.Add(new InvalidResponseAttempt(i, response.Feedback,
						"Claude reply did not contain a yaml fenced block. Raw reply:\n" + response.RawText));
					Log($"Attempt {i}: no yaml block in reply.");
					continue;
				}

				var yaml = response.Yaml!;

				// Generate the assets this descriptor references.
				var requests = AssetManifestExtractor.Extract(response.RawText);
				Log($"Attempt {i}: {requests.Count} asset request(s) in manifest.");
				var assetResults = await _batch.GenerateMissingAsync(
					_registry, requests, _apiKey, _options, _maxConcurrency, _logger, ct);

				foreach (var r in assetResults)
				{
					assetResultsByKey[AssetKey(r.Request)] = r;
				}

				var failedAssets = assetResults.Where(r => !r.Success).ToList();
				foreach (var fa in failedAssets)
				{
					Log($"Attempt {i}: asset '{fa.Request.Id}' ({fa.Request.Type}) failed: {fa.Error}");
				}

				// Write the descriptor. The first time we name the file from its title; after
				// that (fix passes and revisions) we overwrite the same file.
				if (_storedPath == null)
				{
					var title = TryExtractTitle(yaml);
					_storedPath = DescriptorFileWriter.Write(yaml, title);
					Log($"Attempt {i}: wrote {_storedPath}");
				}
				else
				{
					DescriptorFileWriter.WriteTo(yaml, _storedPath);
					Log($"Attempt {i}: overwrote {_storedPath}");
				}

				Log($"Attempt {i}: building...");
				var buildResult = BuildHarness.TryBuild(yaml);
				attempts.Add(new BuildAttempt(i, yaml, response.Feedback, buildResult));

				if (buildResult.Success)
				{
					Log($"Attempt {i}: build succeeded.");
					return new GameWithAssetsResult(
						new SuccessfulGeneration(_storedPath!, attempts), assetResultsByKey.Values.ToList());
				}

				Log($"Attempt {i}: build failed with {buildResult.Errors.Count} error(s).");

				// Feed build errors + failed-asset notes into the next fix pass.
				var combined = new List<string>(buildResult.Errors);
				foreach (var fa in failedAssets)
				{
					combined.Add(
						$"Asset generation failed for id '{fa.Request.Id}' (type '{fa.Request.Type}', " +
						$"path '{fa.Request.ResourcesPath}'): {fa.Error}");
				}
				lastErrors = combined;
			}

			return new GameWithAssetsResult(
				new FailedGeneration(_storedPath, attempts), assetResultsByKey.Values.ToList());
		}

		private static string AssetKey(AssetRequest req) => req.Type + " " + req.ResourcesPath;

		private static string BuildSlug(string userPrompt)
		{
			var slug = DescriptorFileWriter.Sanitise(userPrompt);
			if (!string.IsNullOrEmpty(slug) && slug.Length > MaxSlugLength)
			{
				slug = slug[..MaxSlugLength].Trim('-');
			}

			return string.IsNullOrEmpty(slug)
				? "game-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")
				: slug;
		}

		private static string? TryExtractTitle(string yaml)
		{
			var m = TitleRegex.Match(yaml);
			if (!m.Success)
			{
				return null;
			}

			var value = m.Groups["value"].Value.Trim().Trim('"', '\'');
			return string.IsNullOrWhiteSpace(value) ? null : value;
		}

		private void Log(string message) => _logger.Log(message);
	}
}
