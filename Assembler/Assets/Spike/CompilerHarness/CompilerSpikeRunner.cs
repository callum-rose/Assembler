using System;
using System.IO;
using System.Threading.Tasks;
using Assembler.Building;
using Spike.CompilerHarness.Cases;
using UnityEngine;
using UnityEngine.Networking;

namespace Spike.CompilerHarness
{
	/// <summary>
	/// Throwaway AOT harness. Answers one question — which expression-compiler constructs survive IL2CPP
	/// on iOS and Android — with a per-construct verdict rather than a binary pass/fail.
	///
	/// Two independent halves, two verdicts:
	/// <list type="number">
	/// <item>The flat case list drives <c>ExpressionMethodCompiler</c> directly. Each case must
	/// <b>compile and invoke</b>: AOT can fail at either point, and a compile-only check would miss
	/// the delegate-invocation failures that are the more common IL2CPP symptom.</item>
	/// <item><c>StressTest.yaml</c> goes through <see cref="Builder"/>, covering the
	/// Parsing/Resolving <c>MakeGenericType</c> sites (<c>ValueSourceFactory</c>,
	/// <c>ExpressionSynthesis</c>, <c>TransformContext</c>) that a raw-compiler harness never reaches.</item>
	/// </list>
	///
	/// Readout is <see cref="Debug.Log"/> only — read via the Xcode console and <c>adb logcat</c>. There is
	/// deliberately no on-screen UI: a real IL2CPP failure can be a hard crash that an on-screen report
	/// wouldn't survive, and the pre-execution <c>RUN</c> line is what names the offender when that happens.
	///
	/// Disposal: delete <c>Assets/Spike/</c>, delete <c>Assets/StreamingAssets/StressTest.yaml</c>, and revert
	/// the <c>EditorBuildSettings</c> scene entry. Nothing in a shipping assembly is touched.
	/// </summary>
	public sealed class CompilerSpikeRunner : MonoBehaviour
	{
		[Tooltip("Skip cases before this index. Set it past a case that hard-crashes the player to resume the run.")]
		[SerializeField] private int _startIndex;

		[Tooltip("Also build StressTest.yaml through the full pipeline (the second, descriptor-half verdict).")]
		[SerializeField] private bool _runDescriptorHalf = true;

		[Tooltip("Descriptor file name under StreamingAssets for the descriptor half.")]
		[SerializeField] private string _descriptorFileName = "StressTest.yaml";

		// async void: a Unity lifecycle callback can't return a Task. The whole body is wrapped because an
		// exception escaping an async void is unhandled and can take the player down with no summary line.
		private async void Start()
		{
			try
			{
				LogHeader();

				var (passed, failed) = RunCases();

				if (_runDescriptorHalf)
				{
					var descriptorOk = await RunDescriptorHalfAsync();
					Debug.Log($"COMPILER-SPIKE DESCRIPTOR: {(descriptorOk ? "PASS" : "FAIL")}");
				}
				else
				{
					Debug.Log("COMPILER-SPIKE DESCRIPTOR: SKIPPED");
				}

				// Kept as the final line, and in this exact shape, so a device log can be grepped for it.
				Debug.Log($"COMPILER-SPIKE SUMMARY: {passed} passed, {failed} failed");
			}
			catch (Exception e)
			{
				Debug.LogError($"COMPILER-SPIKE ABORTED: {e}");
			}
		}

		private static void LogHeader()
		{
			Debug.Log("COMPILER-SPIKE START " +
				$"platform={Application.platform} " +
				$"unity={Application.unityVersion} " +
				$"il2cpp={IsIl2Cpp()}");
		}

		// A compile-time check, so the log line states what the running binary actually is rather than
		// what the build settings claimed at author time.
		private static bool IsIl2Cpp()
		{
#if ENABLE_IL2CPP
			return true;
#else
			return false;
#endif
		}

		private (int Passed, int Failed) RunCases()
		{
			var list = new SpikeCaseList();
			AllCases.Register(list);

			var cases = list.Cases;
			var passed = 0;
			var failed = 0;

			Debug.Log($"COMPILER-SPIKE CASES: {cases.Count} total, starting at index {_startIndex}");

			for (var i = 0; i < cases.Count; i++)
			{
				if (i < _startIndex)
				{
					continue;
				}

				var spikeCase = cases[i];

				// Logged BEFORE execution: if this case hard-crashes the process, this is the last line in
				// the device log and it names the offender.
				Debug.Log($"RUN [{i}] {spikeCase.Id}");

				try
				{
					spikeCase.Run();
					passed++;
				}
				catch (Exception e)
				{
					failed++;
					Debug.LogError($"FAIL [{i}] {spikeCase.Id}: {e.GetType().Name}: {e.Message}");
				}
			}

			return (passed, failed);
		}

		private async Task<bool> RunDescriptorHalfAsync()
		{
			Debug.Log($"RUN DESCRIPTOR {_descriptorFileName}");

			try
			{
				var yaml = await ReadStreamingAssetTextAsync(_descriptorFileName);
				await Builder.BuildFromYamlAsync(yaml);
				return true;
			}
			catch (Exception e)
			{
				Debug.LogError($"FAIL DESCRIPTOR {_descriptorFileName}: {e}");
				return false;
			}
		}

		/// <summary>
		/// Reads a StreamingAssets file on every target. On Android the path is a <c>jar:file://</c> URL
		/// into the APK, where <see cref="File"/> silently fails — the descriptor half would then go red on
		/// Android for a reason that has nothing to do with AOT, which is exactly the false positive this
		/// spike exists to avoid. <c>GameBootstrap</c> uses the <see cref="File"/> path directly because it
		/// was written for iOS only.
		/// </summary>
		private static async Task<string> ReadStreamingAssetTextAsync(string fileName)
		{
			var path = Path.Combine(Application.streamingAssetsPath, fileName);

			if (!path.Contains("://"))
			{
				if (!File.Exists(path))
				{
					throw new FileNotFoundException(
						$"Descriptor not found at '{path}'. Ensure it is under Assets/StreamingAssets and in the build.",
						path);
				}

				return File.ReadAllText(path);
			}

			using var request = UnityWebRequest.Get(path);
			var operation = request.SendWebRequest();

			while (!operation.isDone)
			{
				await Task.Yield();
			}

			if (request.result != UnityWebRequest.Result.Success)
			{
				throw new IOException($"Failed to read '{path}' from StreamingAssets: {request.error}");
			}

			return request.downloadHandler.text;
		}
	}
}
