using System.Threading;
using Assembler.Generation.Verification;
using UnityEngine;

namespace Assembler.Generation.Runtime
{
	public sealed class PlayerBuildSmoke : MonoBehaviour, IGeneratorLogger
	{
		[Tooltip("Anthropic API key. Hardcode for the smoke test only — do not ship with this populated.")]
		[SerializeField] private string apiKey = string.Empty;

		[TextArea(3, 10)]
		[SerializeField] private string userPrompt = "Make me a simple Breakout clone.";

		[SerializeField] private int maxAttempts = 3;

		private CancellationTokenSource? _cts;

		private async void Start()
		{
			if (string.IsNullOrWhiteSpace(apiKey))
			{
				Debug.LogError("PlayerBuildSmoke: apiKey is empty. Populate it in the inspector before building.");
				return;
			}

			_cts = new CancellationTokenSource();
			var orchestrator = GenerationOrchestrator.CreateDefault(apiKey, this);
			Debug.Log("PlayerBuildSmoke: starting generation...");
			var result = await orchestrator.GenerateAsync(userPrompt, maxAttempts, _cts.Token);
			Debug.Log(result.Success
				? $"PlayerBuildSmoke: SUCCESS — descriptor at {result.YamlPath}"
				: $"PlayerBuildSmoke: FAILED after {result.Attempts.Count} attempt(s).");
		}

		private void OnDestroy()
		{
			_cts?.Cancel();
			_cts?.Dispose();
		}

		public void Log(string message)
		{
			Debug.Log("[PlayerBuildSmoke] " + message);
		}
	}
}
