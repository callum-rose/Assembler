using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Editor
{
	// Headless entry point for running the EditMode test suites without the editor UI — the same
	// tests you would run from Window > General > Test Runner. Invoked from Tools/run-tests.sh via:
	//   Unity -batchmode -nographics -projectPath <project>
	//         -executeMethod Editor.TestBatch.RunEditModeTests -logFile -
	//
	// IMPORTANT: do NOT pass -quit. TestRunnerApi.Execute is asynchronous (tests run across many
	// editor update frames); -quit would kill the editor before they finish. Instead this class
	// keeps the editor alive and calls EditorApplication.Exit() itself from RunFinished, with exit
	// code 0 when everything passes and 1 when anything fails or errors — so the runner script and
	// Claude can detect the outcome from the exit code as well as the logged summary.
	//
	// Optional filtering is read from the command line (forwarded by run-tests.sh):
	//   -testAssembly <name>   restrict to one or more assemblies (repeatable), e.g. Tests.Compiler
	//   -testFilter <regex>    restrict to tests whose full name matches the regex (repeatable)
	//   -testCategory <name>   restrict to tests with the given [Category] (repeatable)
	public static class TestBatch
	{
		// Rooted in statics so the API object and callbacks survive across the async run and aren't
		// garbage-collected mid-flight.
		private static TestRunnerApi? _api;
		private static Callbacks? _callbacks;

		public static void RunEditModeTests()
		{
			try
			{
				string[] args = Environment.GetCommandLineArgs();

				var filter = new Filter { testMode = TestMode.EditMode };

				string[] assemblies = EditorBatchCli.ArgValues(args, "-testAssembly").ToArray();
				if (assemblies.Length > 0)
					filter.assemblyNames = assemblies;

				string[] nameRegexes = EditorBatchCli.ArgValues(args, "-testFilter").ToArray();
				if (nameRegexes.Length > 0)
					filter.groupNames = nameRegexes;

				string[] categories = EditorBatchCli.ArgValues(args, "-testCategory").ToArray();
				if (categories.Length > 0)
					filter.categoryNames = categories;

				_api = ScriptableObject.CreateInstance<TestRunnerApi>();
				_callbacks = new Callbacks();
				_api.RegisterCallbacks(_callbacks);

				var scope = DescribeScope(assemblies, nameRegexes, categories);
				Debug.Log($"TestBatch: starting EditMode test run ({scope}).");

				_api.Execute(new ExecutionSettings(filter));
				// Returns immediately; Callbacks.RunFinished exits the editor when the run completes.
			}
			catch (Exception e)
			{
				Debug.LogError("TestBatch failed to start: " + e);
				EditorApplication.Exit(1);
			}
		}

		private static string DescribeScope(string[] assemblies, string[] nameRegexes, string[] categories)
		{
			var parts = new List<string>();
			if (assemblies.Length > 0)
				parts.Add("assemblies=" + string.Join(",", assemblies));
			if (nameRegexes.Length > 0)
				parts.Add("filter=" + string.Join(",", nameRegexes));
			if (categories.Length > 0)
				parts.Add("category=" + string.Join(",", categories));
			return parts.Count > 0 ? string.Join(" ", parts) : "all EditMode tests";
		}

		private sealed class Callbacks : ICallbacks
		{
			public void RunStarted(ITestAdaptor testsToRun)
			{
			}

			public void TestStarted(ITestAdaptor test)
			{
			}

			public void TestFinished(ITestResultAdaptor result)
			{
			}

			public void RunFinished(ITestResultAdaptor result)
			{
				try
				{
					int passed = result.PassCount;
					int failed = result.FailCount;
					int skipped = result.SkipCount;
					int inconclusive = result.InconclusiveCount;

					var failures = new List<ITestResultAdaptor>();
					CollectFailures(result, failures);

					var sb = new StringBuilder();
					sb.AppendLine("================ TestBatch results ================");
					sb.AppendLine($"Passed: {passed}  Failed: {failed}  Skipped: {skipped}  Inconclusive: {inconclusive}");

					if (failures.Count > 0)
					{
						sb.AppendLine();
						sb.AppendLine($"Failures ({failures.Count}):");
						foreach (ITestResultAdaptor f in failures)
						{
							sb.AppendLine($"  ✗ {f.FullName}");
							if (!string.IsNullOrWhiteSpace(f.Message))
								sb.AppendLine("      " + f.Message.Trim().Replace("\n", "\n      "));
							if (!string.IsNullOrWhiteSpace(f.StackTrace))
								sb.AppendLine("      " + f.StackTrace.Trim().Replace("\n", "\n      "));
						}
					}

					sb.AppendLine("===================================================");

					// Persist the full NUnit XML alongside the project for later inspection / CI.
					TryWriteXml(result);

					bool ok = failed == 0 && inconclusive == 0;
					if (ok)
						Debug.Log(sb.ToString());
					else
						Debug.LogError(sb.ToString());

					EditorApplication.Exit(ok ? 0 : 1);
				}
				catch (Exception e)
				{
					Debug.LogError("TestBatch: error while reporting results: " + e);
					EditorApplication.Exit(1);
				}
			}

			private static void CollectFailures(ITestResultAdaptor result, List<ITestResultAdaptor> failures)
			{
				bool isLeaf = !result.HasChildren;
				if (isLeaf && result.TestStatus == TestStatus.Failed)
					failures.Add(result);

				if (result.Children == null)
					return;

				foreach (ITestResultAdaptor child in result.Children)
					CollectFailures(child, failures);
			}

			private static void TryWriteXml(ITestResultAdaptor result)
			{
				try
				{
					string dir = Path.Combine(Directory.GetCurrentDirectory(), "TestResults");
					Directory.CreateDirectory(dir);
					string path = Path.Combine(dir, "EditMode-results.xml");
					File.WriteAllText(path, result.ToXml().OuterXml);
					Debug.Log("TestBatch: wrote results XML to " + path);
				}
				catch (Exception e)
				{
					Debug.LogWarning("TestBatch: could not write results XML: " + e.Message);
				}
			}
		}
	}
}
