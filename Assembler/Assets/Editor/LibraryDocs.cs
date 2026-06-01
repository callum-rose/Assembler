using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace Editor
{
	// Generates Markdown docs for the reusable static helpers exposed to descriptor
	// expressions by the Assembler.Libraries assembly (GridMath, plus anything else
	// dropped into Assets/Libraries). Reflects over the same set of types that
	// CompiledExpressionsRegistry auto-registers, and reads each method's
	// <summary>/<param>/<returns> from the compiler-emitted XML doc file.
	public static class LibraryDocs
	{
		private const string AssemblyName = "Assembler.Libraries";

		// DocGen/ is a persistent, non-volatile location written by Assets/Libraries/csc.rsp's
		// -doc flag. Unlike Temp/ (which Unity clears on batch-mode startup), it survives between
		// runs, so headless doc generation finds the XML even when the assembly wasn't recompiled
		// this session. Temp/ and ScriptAssemblies are kept as fallbacks for older layouts.
		private readonly static string[] CandidateXmlPaths =
		{
			"DocGen/Assembler.Libraries.xml",
			"Temp/Assembler.Libraries.xml",
			"Library/ScriptAssemblies/Assembler.Libraries.xml",
		};

		[MenuItem("Assembler/Print Library Docs")]
		private static void DebugLogMarkdown()
		{
			Debug.Log(GenerateMarkdown());
		}

		[MenuItem("Assembler/Generate Library Docs")]
		private static void GenerateLibraryDocs()
		{
			try
			{
				WriteDocs();
				AssetDatabase.Refresh();
			}
			catch (Exception e)
			{
				Debug.LogError(e);
			}
		}

		// Builds the markdown and writes it to disk, returning the output path. Shared by the
		// menu item and the headless DocsBatch entry point. Does not call AssetDatabase.Refresh —
		// callers decide whether/when to refresh.
		internal static string WriteDocs()
		{
			const string outputPath = "Assets/docs/Libraries.md";
			Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
			File.WriteAllText(outputPath, GenerateMarkdown());
			Debug.Log($"Wrote {outputPath}");
			return outputPath;
		}

		private static string GenerateMarkdown()
		{
			var members = XmlDocs.LoadMembers(CandidateXmlPaths);
			var sb = new StringBuilder();
			sb.AppendLine("# Libraries");
			sb.AppendLine();
			sb.AppendLine(
				"Generated from the `Assembler.Libraries` XML doc comments. Every public static method " +
				"of these classes is registered globally with the expression compiler, so descriptor " +
				"expressions can call it by bare name.");
			sb.AppendLine();

			var warnings = new List<string>();

			var libraryTypes = ResolveLibraryTypes(warnings)
				.OrderBy(t => t.Name, StringComparer.Ordinal)
				.ToList();

			foreach (var type in libraryTypes)
			{
				sb.AppendLine($"## `{type.Name}`");

				var typeSummary = FindSummary(members, "T:" + XmlDocs.XmlDocTypeName(type));
				if (!string.IsNullOrWhiteSpace(typeSummary))
				{
					sb.AppendLine(typeSummary!.Trim());
				}

				sb.AppendLine();

				var methods = type
					.GetMethods(BindingFlags.Public | BindingFlags.Static)
					.Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
					.OrderBy(m => m.Name, StringComparer.Ordinal)
					.ThenBy(m => m.GetParameters().Length)
					.ToList();

				if (methods.Count == 0)
				{
					sb.AppendLine("_No public static methods._");
					sb.AppendLine();
					continue;
				}

				foreach (var method in methods)
				{
					AppendMethod(sb, type, method, members, warnings);
				}
			}

			if (warnings.Count > 0)
			{
				sb.AppendLine("---");
				sb.AppendLine();
				sb.AppendLine("## Doc-gen warnings");
				sb.AppendLine();
				foreach (var w in warnings)
				{
					sb.AppendLine($"- {w}");
				}

				Debug.LogWarning("Library doc generation produced warnings:\n" + string.Join("\n", warnings));
			}

			return sb.ToString();
		}

		private static void AppendMethod(
			StringBuilder sb,
			Type type,
			MethodInfo method,
			Dictionary<string, XElement> members,
			List<string> warnings)
		{
			var parameters = method.GetParameters();
			var signature =
				$"{XmlDocs.RenderType(method.ReturnType)} {method.Name}(" +
				string.Join(", ", parameters.Select(p => $"{XmlDocs.RenderType(p.ParameterType)} {p.Name}")) +
				")";

			sb.AppendLine($"### `{signature}`");

			var memberId = XmlDocs.MethodMemberId(method);
			if (!members.TryGetValue(memberId, out var member))
			{
				warnings.Add($"`{type.Name}.{method.Name}`: no XML doc member found for id `{memberId}`.");
				sb.AppendLine($"_No XML docs — add `<summary>` on `{type.Name}.{method.Name}`._");
				sb.AppendLine();
				return;
			}

			var summaryElement = member.Element("summary");
			var summary = summaryElement is null ? null : XmlDocs.Flatten(summaryElement);
			if (!string.IsNullOrWhiteSpace(summary))
			{
				sb.AppendLine(Collapse(summary!));
				sb.AppendLine();
			}

			var paramDocs = member.Elements("param")
				.ToDictionary(p => p.Attribute("name")?.Value ?? "", p => Collapse(XmlDocs.Flatten(p)));

			if (parameters.Length > 0)
			{
				sb.AppendLine("| Parameter | Type | Description |");
				sb.AppendLine("|-----------|------|-------------|");
				foreach (var p in parameters)
				{
					var desc = paramDocs.TryGetValue(p.Name!, out var d) ? d : "";
					sb.AppendLine($"| {p.Name} | {XmlDocs.RenderType(p.ParameterType)} | {desc} |");
				}

				sb.AppendLine();
			}

			var returnsElement = member.Element("returns");
			var returns = returnsElement is null ? null : XmlDocs.Flatten(returnsElement);
			if (method.ReturnType != typeof(void) && !string.IsNullOrWhiteSpace(returns))
			{
				sb.AppendLine($"**Returns** ({XmlDocs.RenderType(method.ReturnType)}): {Collapse(returns!)}");
				sb.AppendLine();
			}
		}

		private static string? FindSummary(Dictionary<string, XElement> members, string key)
		{
			if (!members.TryGetValue(key, out var member))
			{
				return null;
			}

			var summary = member.Element("summary");
			return summary is null ? null : XmlDocs.Flatten(summary);
		}

		// Resolves the public, non-generic classes of the Assembler.Libraries assembly —
		// the same set CompiledExpressionsRegistry registers static methods from.
		private static IEnumerable<Type> ResolveLibraryTypes(List<string> warnings)
		{
			var assembly = AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault(a => a.GetName().Name == AssemblyName);

			if (assembly is null)
			{
				warnings.Add($"Assembly `{AssemblyName}` not found in the current AppDomain.");
				return Array.Empty<Type>();
			}

			return assembly.GetExportedTypes().Where(t => t.IsClass && !t.IsGenericTypeDefinition);
		}

		// Flattens the whitespace in an XML doc value (which is typically indented and
		// wrapped) into a single Markdown-table-safe line.
		private static string Collapse(string text) =>
			string.Join(" ", text.Split(new[] { '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(s => s.Trim()))
			.Trim();
	}
}
