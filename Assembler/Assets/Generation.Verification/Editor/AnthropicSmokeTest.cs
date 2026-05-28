#if UNITY_EDITOR
using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Assembler.Generation.Verification.Editor
{
	public static class AnthropicSmokeTest
	{
		private const string ApiKeyPref = "Assembler.Generation.ApiKey";

		[MenuItem("Assembler/Smoke Test Anthropic Client")]
		public static void Run()
		{
			var apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
			if (string.IsNullOrWhiteSpace(apiKey))
			{
				Debug.LogError(
					$"Smoke test aborted — no API key. Set EditorPrefs key '{ApiKeyPref}' first " +
					"(the Generate Game Descriptor window in a later phase will provide a UI for this).");
				return;
			}

			RunAsync(apiKey);
		}

		private static async void RunAsync(string apiKey)
		{
			try
			{
				var client = new AnthropicClient(apiKey);
				const string systemPrompt = "You are a friendly assistant. Reply with a single short sentence.";
				var messages = new[]
				{
					new AnthropicMessage("user", "Say hi."),
				};
				Debug.Log("AnthropicSmokeTest: sending request...");
				var reply = await client.SendAsync(systemPrompt, messages, CancellationToken.None);
				Debug.Log($"AnthropicSmokeTest reply:\n{reply}");
			}
			catch (Exception ex)
			{
				Debug.LogError($"AnthropicSmokeTest failed: {ex}");
			}
		}
	}
}
#endif
