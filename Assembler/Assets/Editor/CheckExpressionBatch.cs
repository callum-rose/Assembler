using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assembler.Compiler.Compiler;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace Editor
{
	// Pipeline + menu entry points for the standalone ExpressionMethodCompiler check: feeds expressions
	// straight through the compiler and reports compile errors (with the positions the compiler embeds in
	// its messages) WITHOUT booting a game. This is the cheap, sub-second companion to `validate_game` for
	// exactly the failure class the expression-compiler authoring guidance warns about — bad expression
	// syntax that otherwise only surfaces at runtime.
	//
	// Two input modes (combinable in one run):
	//   - raw snippet:      --expr '<C# body>', compiled with --return-type (default 'float') and any
	//                       --args '<type>:<name>,...' pairs, exactly as a descriptor's ExpressionInfo would be.
	//   - descriptor sweep: --targets <file-or-dir>,... extracts EVERY expression embedded in each
	//                       descriptor (named + inline) by running it through deserialise + transform
	//                       (no resolve/instantiate), then compiles each.
	// With neither it sweeps every descriptor under Assets/ExampleGameDescriptors as a batch audit.
	//
	// Reached from the command line as `unity command check_expression`. A failure to compile fails the
	// command (non-zero exit), so the outcome is readable from the exit status as well as the report.
	public static class CheckExpressionBatch
	{
		private const string DefaultDescriptorDir = "Assets/ExampleGameDescriptors";
		private const string DefaultReturnType = "float";

		// Pipeline entry point, reached as `unity command check_expression`. Keeps both of the script's
		// input modes: --expr compiles raw snippets against --return-type / --args, --targets audits the
		// expressions embedded in descriptors, and passing neither audits the example descriptors.
		// Compilation is in-process and cheap, so this comfortably fits the 60s main-thread budget.
		[CliCommand("check_expression",
			"Compile expressions via ExpressionMethodCompiler without booting a game — raw snippets or "
			+ "every !expr / Expressions: body in a descriptor. Fails the command when any fails to compile.",
			Tags = new[] { "assembler/validation" })]
		public static PipelineReport CheckExpressionCommand(
			[CliArg("targets", "Comma-separated descriptor files or directories to audit. "
				+ "Defaults to Assets/ExampleGameDescriptors when --expr is also omitted.")]
			string? targets = null,
			// Deliberately NOT comma-separated, unlike the other list arguments: expression bodies
			// contain commas themselves (`Clamp(x, 0f, 1f)` would split into three fragments that each
			// fail to compile). One expression per call; they are sub-second, so calling repeatedly is
			// cheap, and a descriptor sweep covers the bulk case.
			[CliArg("expr", "A single raw expression body to compile directly, e.g. 'Clamp(x, 0f, 1f)'.")]
			string? expr = null,
			[CliArg("return-type", "Return type for raw --expr snippets, e.g. float, vector, bool.")]
			string returnType = DefaultReturnType,
			[CliArg("args", "Comma-separated argument specs for raw --expr snippets, "
				+ "each 'type:name', e.g. 'vector:vel,float:dt'.")]
			string? args = null)
		{
			EditorPipelineCli.RequireFreshAssets();

			var rawExprs = new List<string>();
			if (!string.IsNullOrWhiteSpace(expr))
			{
				rawExprs.Add(expr!);
			}

			List<string> descriptorPaths = EditorPipelineCli.SplitTargets(targets);
			List<string> argSpecs = EditorPipelineCli.SplitTargets(args);

			// Same default as the command line: with no input at all, audit the example descriptors.
			if (rawExprs.Count == 0 && descriptorPaths.Count == 0)
			{
				descriptorPaths.Add(DefaultDescriptorDir);
			}

			bool ok = Run(rawExprs, returnType, argSpecs, descriptorPaths, out string report);
			return EditorPipelineCli.Complete(report, ok);
		}

		// In-editor convenience: audit every example descriptor's expressions and log the report.
		[MenuItem("Assembler/Check Expressions (example descriptors)")]
		private static void CheckExpressionsMenu()
		{
			bool ok = Run(new List<string>(), DefaultReturnType, new List<string>(),
				new List<string> { DefaultDescriptorDir }, out string report);
			EditorBatchCli.LogReport(report, ok);
		}

		// Builds the combined report across both input modes. Returns true when every expression compiled.
		private static bool Run(IReadOnlyList<string> rawExprs, string returnType, IReadOnlyList<string> argSpecs,
			IReadOnlyList<string> descriptorPaths, out string report)
		{
			var sb = new StringBuilder();
			sb.AppendLine("===== check-expression report =====");

			int total = 0;
			int failed = 0;
			// Descriptors we couldn't even read (bad path, malformed YAML, transform error) are tracked
			// separately from expression-compile failures: they're a different failure class (validate-yaml/
			// validate-game own structural problems), but they still mean the requested check couldn't be
			// completed, so they also force a non-zero exit.
			int unreadable = 0;

			if (rawExprs.Count > 0)
			{
				sb.AppendLine($"Raw expressions (returnType: {returnType}):");
				foreach (var (line, ok) in CheckRawExpressions(rawExprs, returnType, argSpecs))
				{
					total++;
					if (!ok)
					{
						failed++;
					}

					sb.AppendLine(line);
				}

				sb.AppendLine();
			}

			foreach (var target in descriptorPaths)
			{
				List<string> files;
				try
				{
					files = EditorBatchCli.CollectYamlFiles(new[] { target });
				}
				catch (Exception e)
				{
					unreadable++;
					sb.AppendLine($"SKIP  {target}  ({e.Message})");
					continue;
				}

				foreach (var file in files)
				{
					string rel = EditorBatchCli.ToProjectRelative(file);
					var (lines, count, fails, couldNotRead) = CheckDescriptor(file, rel);
					total += count;
					failed += fails;
					unreadable += couldNotRead ? 1 : 0;
					foreach (var line in lines)
					{
						sb.AppendLine(line);
					}
				}
			}

			sb.AppendLine();
			sb.AppendLine(failed == 0
				? $"All {total} expression(s) compiled cleanly."
				: $"{failed} of {total} expression(s) failed to compile.");
			if (unreadable > 0)
			{
				sb.AppendLine($"{unreadable} descriptor(s) could not be read (reported above).");
			}

			sb.AppendLine("===== end report =====");

			report = sb.ToString();
			return failed == 0 && unreadable == 0;
		}

		// Compiles each raw snippet as a one-off ExpressionInfo with the shared return type and arguments.
		private static IEnumerable<(string line, bool ok)> CheckRawExpressions(
			IReadOnlyList<string> rawExprs, string returnType, IReadOnlyList<string> argSpecs)
		{
			var typeRegistry = BuiltInTypeRegistry.Default;

			// Validate the declared types once up front so every snippet gets the same clear message rather
			// than an opaque dictionary-miss from deep inside the compiler.
			if (!typeRegistry.ContainsKey(returnType))
			{
				yield return ($"  FAIL  (unknown returnType '{returnType}'; known: {KnownTypes(typeRegistry)})", false);
				yield break;
			}

			IReadOnlyList<(string type, string name)> arguments;
			if (!TryParseArgs(argSpecs, typeRegistry, out arguments, out string argError))
			{
				yield return ($"  FAIL  ({argError})", false);
				yield break;
			}

			for (int i = 0; i < rawExprs.Count; i++)
			{
				var info = new ExpressionInfo($"expr#{i + 1}", arguments, returnType,
					Array.Empty<string>(), Array.Empty<string>(), WrapBody(rawExprs[i]));

				var registry = new CompiledExpressionsRegistry(typeRegistry, new ExpressionMethodCompiler());
				var result = registry.CompileAndRegisterAllBestEffort(new[] { info })[0];

				yield return result.Success
					? ($"  OK    expr#{i + 1}", true)
					: ($"  FAIL  expr#{i + 1}\n        {Indent(result.Error!)}", false);
			}
		}

		// Extracts and compiles every expression embedded in one descriptor. Parsing/transforming the
		// descriptor is required to discover its expressions (named + inline); a failure there is reported
		// as a single descriptor-level failure since no expressions could be recovered.
		private static (IReadOnlyList<string> lines, int count, int failed, bool couldNotRead) CheckDescriptor(
			string file, string rel)
		{
			GameInfo gameInfo;
			try
			{
				var dto = new GameFileParser().Parse(File.ReadAllText(file));
				gameInfo = Transformer.Transform(dto);
			}
			catch (Exception e)
			{
				return (new[] { $"SKIP  {rel}  (could not extract expressions — {FirstLine(e.Message)})" }, 0, 0, true);
			}

			var expressions = gameInfo.Expressions;
			if (expressions.Count == 0)
			{
				return (new[] { $"{rel}: no expressions" }, 0, 0, false);
			}

			var registry = new CompiledExpressionsRegistry(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());
			var results = registry.CompileAndRegisterAllBestEffort(expressions);

			int failed = results.Count(r => !r.Success);
			var lines = new List<string> { $"{rel}: {expressions.Count} expression(s)" };
			foreach (var result in results)
			{
				lines.Add(result.Success
					? $"  OK    {result.Info.Id}"
					: $"  FAIL  {result.Info.Id}\n        {Indent(result.Error!)}");
			}

			return (lines, expressions.Count, failed, false);
		}

		// Parses "-arg <type>:<name>" specs into typed arguments, validating each type against the registry.
		private static bool TryParseArgs(IReadOnlyList<string> argSpecs, IReadOnlyDictionary<string, Type> typeRegistry,
			out IReadOnlyList<(string type, string name)> arguments, out string error)
		{
			var parsed = new List<(string type, string name)>();
			foreach (var spec in argSpecs)
			{
				int colon = spec.IndexOf(':');
				if (colon <= 0 || colon == spec.Length - 1)
				{
					arguments = parsed;
					error = $"malformed -arg '{spec}' (expected '<type>:<name>')";
					return false;
				}

				string type = spec.Substring(0, colon).Trim();
				string name = spec.Substring(colon + 1).Trim();
				if (!typeRegistry.ContainsKey(type))
				{
					arguments = parsed;
					error = $"unknown -arg type '{type}'; known: {KnownTypes(typeRegistry)}";
					return false;
				}

				parsed.Add((type, name));
			}

			arguments = parsed;
			error = string.Empty;
			return true;
		}

		// Mirrors Transformer.WrapInlineBody so a raw snippet behaves exactly like an inline `Do:` body: a
		// bare expression (no ';') becomes an implicit `return <body>;`, while a multi-statement body
		// (containing ';') is handed to the compiler verbatim (the author writes their own return).
		private static string WrapBody(string body) =>
			body.Contains(';') ? body : $"return {body};";

		private static string KnownTypes(IReadOnlyDictionary<string, Type> typeRegistry) =>
			string.Join(", ", typeRegistry.Keys.OrderBy(k => k, StringComparer.Ordinal));

		// Indents continuation lines of a multi-line error so they align under the first.
		private static string Indent(string message) => message.Replace("\n", "\n        ");

		private static string FirstLine(string message) =>
			message.Split('\n')[0].Trim();
	}
}
