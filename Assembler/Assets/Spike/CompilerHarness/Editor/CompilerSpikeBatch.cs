using System;
using System.Text;
using Spike.CompilerHarness.Cases;
using UnityEditor;
using UnityEngine;

namespace Spike.CompilerHarness.Editor
{
	/// <summary>
	/// Runs the flat case list in the editor without entering Play mode, so the corpus can be verified
	/// headlessly (batch mode) while iterating. This exists to satisfy the handover gate — every case must
	/// be green in the editor before the user builds — because a case that is merely mis-authored would
	/// otherwise show up on-device as a fake AOT finding.
	///
	/// This covers the <b>flat-case half only</b>. The descriptor half needs the runtime scene, and a
	/// device build is the only thing that actually measures AOT: a green run here says the corpus is
	/// well-formed, not that it survives IL2CPP.
	///
	/// Editor-only, and disposed with the rest of <c>Assets/Spike/</c>.
	/// </summary>
	public static class CompilerSpikeBatch
	{
		[MenuItem("Assembler/Spike/Run Compiler Harness Cases (editor)")]
		public static void RunCasesFromMenu() => RunCases();

		/// <summary>Batch entry point. Exits non-zero when any case fails, so a shell can gate on it.</summary>
		public static void RunCases()
		{
			var failures = 0;
			var passed = 0;
			var report = new StringBuilder();

			var list = new SpikeCaseList();
			AllCases.Register(list);

			foreach (var spikeCase in list.Cases)
			{
				try
				{
					spikeCase.Run();
					passed++;
				}
				catch (Exception e)
				{
					failures++;
					report.AppendLine($"FAIL {spikeCase.Id}: {e.GetType().Name}: {e.Message}");
				}
			}

			Debug.Log($"COMPILER-SPIKE EDITOR CASES: {list.Cases.Count} total");

			if (failures > 0)
			{
				Debug.Log($"COMPILER-SPIKE EDITOR FAILURES:\n{report}");
			}

			Debug.Log($"COMPILER-SPIKE SUMMARY: {passed} passed, {failures} failed");

			if (Application.isBatchMode)
			{
				EditorApplication.Exit(failures > 0 ? 1 : 0);
			}
		}
	}
}
