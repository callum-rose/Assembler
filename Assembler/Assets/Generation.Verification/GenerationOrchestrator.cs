using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Generation.Verification
{

	public sealed class GenerationOrchestrator
	{
		private readonly static Regex TitleRegex = new(
			@"(?im)^\s*title\s*:\s*(?<value>.+?)\s*$",
			RegexOptions.Compiled);
		
		private readonly Func<GameDescriptorGenerator> _generatorFactory;
		private readonly Func<string, BuildResult> _builder;
		private readonly IGeneratorLogger? _logger;

		public static GenerationOrchestrator CreateDefault(string apiKey, IGeneratorLogger? logger = null)
		{
			return new GenerationOrchestrator(
				() => new GameDescriptorGenerator(new AnthropicClient(apiKey)),
				BuildHarness.TryBuild,
				logger);
		}

		private GenerationOrchestrator(
			Func<GameDescriptorGenerator> generatorFactory,
			Func<string, BuildResult>? builder = null,
			IGeneratorLogger? logger = null)
		{
			_generatorFactory = generatorFactory;
			_builder = builder ?? BuildHarness.TryBuild;
			_logger = logger;
		}

		public async Task<GenerationResult> GenerateAsync(
			string userPrompt,
			int maxAttempts,
			CancellationToken cancellationToken)
		{
			if (maxAttempts < 1) maxAttempts = 1;

			var attempts = new List<Attempt>();
			var generator = _generatorFactory();
			string? storedPath = null;

			GeneratorResponse? lastResponse = null;
			for (var i = 1; i <= maxAttempts; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				GeneratorResponse response;
				try
				{
					Log($"Attempt {i}: requesting descriptor from Claude...");
					response = i == 1
						? await generator.RequestInitialAsync(userPrompt, cancellationToken)
						: await generator.RequestFixAsync(lastResponse!.Yaml ?? string.Empty,
							attempts[^1] is BuildAttempt last ? last.BuildResult.Errors : Array.Empty<string>(), cancellationToken);
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

				if (storedPath == null)
				{
					var title = TryExtractTitle(response.Yaml);
					storedPath = DescriptorFileWriter.Write(response.Yaml, title);
					Log($"Attempt {i}: wrote {storedPath}");
				}
				else
				{
					DescriptorFileWriter.WriteTo(response.Yaml, storedPath);
					Log($"Attempt {i}: overwrote {storedPath}");
				}

				Log($"Attempt {i}: building...");
				var buildResult = _builder(response.Yaml);
				attempts.Add(new BuildAttempt(i, response.Yaml, response.Feedback, buildResult));

				if (buildResult.Success)
				{
					Log($"Attempt {i}: build succeeded.");
					return new SuccessfulGeneration(storedPath!, attempts);
				}

				Log($"Attempt {i}: build failed with {buildResult.Errors.Count} error(s).");
			}

			return new FailedGeneration(storedPath, attempts);
		}

		private void Log(string message)
		{
			_logger?.Log(message);
		}

		internal static string? TryExtractTitle(string yaml)
		{
			var m = TitleRegex.Match(yaml);
			if (!m.Success) return null;
			var value = m.Groups["value"].Value.Trim().Trim('"', '\'');
			return string.IsNullOrWhiteSpace(value) ? null : value;
		}
	}
}
