using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Assembler.Building;
using Assembler.Parsing.Info;
using UnityEditor;
using UnityEngine;
using BehaviourRegistry = Assembler.Parsing.BehaviourRegistry;

namespace Editor
{
	public static class BehaviourDocs
	{
		private readonly static string[] CandidateXmlPaths =
		{
			"Temp/Assembler.Behaviours.xml",
			"Library/ScriptAssemblies/Assembler.Behaviours.xml",
		};

		[MenuItem("Assembler/Print Behaviour Docs")]
		private static void DebugLogMarkdown()
		{
			Debug.Log(GenerateMarkdown());
		}

		[MenuItem("Assembler/Generate Behaviour Docs")]
		private static void GenerateBehaviourDocs()
		{
			try
			{
				const string outputPath = "Assets/docs/Behaviours.md";
				Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
				File.WriteAllText(outputPath, GenerateMarkdown());
				AssetDatabase.Refresh();
				Debug.Log($"Wrote {outputPath}");
			}
			catch (Exception e)
			{
				Debug.LogError(e);
			}
		}

		private static string GenerateMarkdown()
		{
			var membersByKey = XmlDocs.LoadMembers(CandidateXmlPaths);
			var sb = new StringBuilder();
			sb.AppendLine("# Behaviours");
			sb.AppendLine();
			sb.AppendLine("Generated from `Assembler.Behaviours` XML doc comments. Each behaviour's description, property meanings, and trigger outputs are authored on the corresponding `GameBehaviour` MonoBehaviour; property names and types are reflected from the matching `*Info` record.");
			sb.AppendLine();

			var warnings = new List<string>();

			foreach (var (name, factory) in BehaviourRegistry.All)
			{
				var infoType = factory.Method.DeclaringType;
				if (infoType is null)
				{
					warnings.Add($"`{name}`: cannot resolve Info type from factory.");
					continue;
				}

				if (!GameBehaviourFactory.MonoBehaviourByInfo.TryGetValue(infoType, out var monoType))
				{
					warnings.Add($"`{name}`: no MonoBehaviour mapping for `{infoType.Name}` (skipped).");
					continue;
				}

				var summary = FindSummary(monoType, membersByKey);
				var (propsDocs, outputsDocs) = FindRemarksSections(monoType, membersByKey);
				var infoProps = GetInfoProperties(infoType);

				ValidateProps(name, infoType, monoType, infoProps, propsDocs, warnings);

				sb.AppendLine($"## `{name}`");
				if (!string.IsNullOrWhiteSpace(summary))
				{
					sb.AppendLine(summary!.Trim());
				}
				else
				{
					sb.AppendLine("_No summary — add `<summary>` on " + monoType.Name + "._");
				}
				sb.AppendLine();

				if (infoProps.Count > 0)
				{
					sb.AppendLine("### Properties");
					sb.AppendLine();
					sb.AppendLine("| Name | Type | Description |");
					sb.AppendLine("|------|------|-------------|");
					foreach (var prop in infoProps)
					{
						var doc = propsDocs.TryGetValue(prop.YamlName, out var d) ? d : new PropDoc(null, null);
						var renderedType = doc.TypeOverride ?? XmlDocs.RenderType(prop.Type);
						var desc = doc.Description ?? "";
						sb.AppendLine($"| {prop.YamlName} | {renderedType} | {desc} |");
					}
					sb.AppendLine();
				}
				else
				{
					sb.AppendLine("No properties.");
					sb.AppendLine();
				}

				if (outputsDocs.Count > 0)
				{
					sb.AppendLine("### Outputs");
					sb.AppendLine();
					sb.AppendLine("| Name | Type | Description |");
					sb.AppendLine("|------|------|-------------|");
					foreach (var (outName, outDoc) in outputsDocs)
					{
						sb.AppendLine($"| {outName} | {outDoc.TypeOverride ?? ""} | {outDoc.Description ?? ""} |");
					}
					sb.AppendLine();
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
				Debug.LogWarning("Behaviour doc generation produced warnings:\n" + string.Join("\n", warnings));
			}

			return sb.ToString();
		}

		// ----------------------------------------------------------------------
		// XML doc loading
		// ----------------------------------------------------------------------

		// Walk the type chain (closed → open generic base → … ) until we find a <member name="T:...">
		// matching, or run out. This lets us author docs on a generic base like
		// VariableSetterBehaviour<T> and have closed subclasses (Vector3Setter, IntSetter, …) inherit them.
		private static string? FindSummary(Type monoType, Dictionary<string, XElement> membersByKey)
		{
			foreach (var member in WalkTypeXmlMembers(monoType, membersByKey))
			{
				var summary = member.Element("summary");
				if (summary is not null)
				{
					return summary.Value;
				}
			}

			return null;
		}

		private static (Dictionary<string, PropDoc> Properties, List<(string Name, PropDoc Doc)> Outputs)
			FindRemarksSections(Type monoType, Dictionary<string, XElement> membersByKey)
		{
			foreach (var member in WalkTypeXmlMembers(monoType, membersByKey))
			{
				var remarks = member.Element("remarks");
				if (remarks is null)
				{
					continue;
				}

				var text = remarks.Value;
				return ParseRemarksBlocks(text);
			}

			return (new Dictionary<string, PropDoc>(), new List<(string, PropDoc)>());
		}

		private static IEnumerable<XElement> WalkTypeXmlMembers(Type type, Dictionary<string, XElement> membersByKey)
		{
			for (var t = type; t is not null && t != typeof(object) && t != typeof(MonoBehaviour); t = t.BaseType)
			{
				var key = "T:" + XmlDocs.XmlDocTypeName(t);
				if (membersByKey.TryGetValue(key, out var member))
				{
					yield return member;
				}
			}
		}

		// ----------------------------------------------------------------------
		// Remarks parsing
		// ----------------------------------------------------------------------

		private sealed record PropDoc(string? TypeOverride, string? Description);

		private readonly static Regex SectionHeader =
			new(@"^\s*(Properties|Outputs)\s*:\s*$", RegexOptions.IgnoreCase);

		// Line forms accepted:
		//   Name: description
		//   Name [TypeOverride]: description
		// Anything else is ignored.
		private readonly static Regex EntryLine =
			new(@"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(\[(?<type>[^\]]+)\])?\s*:\s*(?<desc>.*?)\s*$");

		private static (Dictionary<string, PropDoc> Properties, List<(string Name, PropDoc Doc)> Outputs)
			ParseRemarksBlocks(string text)
		{
			var properties = new Dictionary<string, PropDoc>();
			var outputs = new List<(string, PropDoc)>();
			string? currentSection = null;

			foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
			{
				if (string.IsNullOrWhiteSpace(rawLine))
				{
					continue;
				}

				var headerMatch = SectionHeader.Match(rawLine);
				if (headerMatch.Success)
				{
					currentSection = headerMatch.Groups[1].Value.ToLowerInvariant();
					continue;
				}

				if (currentSection is null)
				{
					continue;
				}

				var entryMatch = EntryLine.Match(rawLine);
				if (!entryMatch.Success)
				{
					continue;
				}

				var name = entryMatch.Groups["name"].Value;
				var typeOverride = entryMatch.Groups["type"].Success ? entryMatch.Groups["type"].Value.Trim() : null;
				var desc = entryMatch.Groups["desc"].Value;

				if (currentSection == "properties")
				{
					properties[name] = new PropDoc(typeOverride, desc);
				}
				else if (currentSection == "outputs")
				{
					outputs.Add((name, new PropDoc(typeOverride, desc)));
				}
			}

			return (properties, outputs);
		}

		// ----------------------------------------------------------------------
		// Info reflection
		// ----------------------------------------------------------------------

		private sealed record InfoProp(string YamlName, Type Type);

		private static IReadOnlyList<InfoProp> GetInfoProperties(Type infoType)
		{
			var ctor = infoType.GetConstructors().FirstOrDefault();
			if (ctor is null)
			{
				return Array.Empty<InfoProp>();
			}

			var result = new List<InfoProp>();
			foreach (var p in ctor.GetParameters())
			{
				if (p.Name == "Id" || p.Name == "Listeners")
				{
					continue;
				}

				// Find the auto-generated property with the same name to read YamlName attribute
				var property = infoType.GetProperty(p.Name!);
				var yamlName = property?.GetCustomAttribute<YamlNameAttribute>()?.Name ?? p.Name!;
				result.Add(new InfoProp(yamlName, UnwrapValueSource(p.ParameterType)));
			}

			return result;
		}

		private static Type UnwrapValueSource(Type t)
		{
			if (t.IsGenericType && t.GetGenericTypeDefinition().Name.StartsWith("ValueSource"))
			{
				return t.GetGenericArguments()[0];
			}

			return t;
		}

		// ----------------------------------------------------------------------
		// Validation
		// ----------------------------------------------------------------------

		private static void ValidateProps(
			string behaviourName,
			Type infoType,
			Type monoType,
			IReadOnlyList<InfoProp> infoProps,
			Dictionary<string, PropDoc> propsDocs,
			List<string> warnings)
		{
			var infoNames = new HashSet<string>(infoProps.Select(p => p.YamlName));
			var docNames = new HashSet<string>(propsDocs.Keys);

			foreach (var missing in infoNames.Except(docNames))
			{
				warnings.Add(
					$"`{behaviourName}`: property `{missing}` on `{infoType.Name}` is missing from `{monoType.Name}`'s `Properties:` block.");
			}

			foreach (var extra in docNames.Except(infoNames))
			{
				warnings.Add(
					$"`{behaviourName}`: `{monoType.Name}` documents `{extra}` in its `Properties:` block but `{infoType.Name}` has no such property.");
			}
		}
	}
}
