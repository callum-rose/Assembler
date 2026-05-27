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
			var membersByKey = LoadXmlDocMembers();
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

				Type? monoType = null;
				string? summary;
				Dictionary<string, PropDoc> propsDocs;
				List<(string Name, PropDoc Doc)> outputsDocs;

				if (GameBehaviourFactory.MonoBehaviourByInfo.TryGetValue(infoType, out var resolvedMonoType))
				{
					monoType = resolvedMonoType;
					summary = FindSummary(monoType, membersByKey);
					(propsDocs, outputsDocs) = FindRemarksSections(monoType, membersByKey);
				}
				else if (HandAuthoredDocs.TryGetValue(name, out var handDoc))
				{
					summary = handDoc.Summary;
					propsDocs = handDoc.Properties.ToDictionary(p => p.Key, p => p.Value);
					outputsDocs = new List<(string, PropDoc)>();
				}
				else
				{
					warnings.Add($"`{name}`: no MonoBehaviour mapping for `{infoType.Name}` (skipped).");
					continue;
				}

				var infoProps = GetInfoProperties(infoType);

				ValidateProps(name, infoType, monoType, infoProps, propsDocs, warnings);

				sb.AppendLine($"## `{name}`");
				if (!string.IsNullOrWhiteSpace(summary))
				{
					sb.AppendLine(summary!.Trim());
				}
				else
				{
					sb.AppendLine("_No summary — add `<summary>` on " + (monoType?.Name ?? infoType.Name) + "._");
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
						var renderedType = doc.TypeOverride ?? RenderType(prop.Type);
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

		private static Dictionary<string, XElement> LoadXmlDocMembers()
		{
			var result = new Dictionary<string, XElement>();
			XDocument? doc = null;
			foreach (var p in CandidateXmlPaths)
			{
				if (File.Exists(p))
				{
					doc = XDocument.Load(p);
					break;
				}
			}

			if (doc is null)
			{
				Debug.LogWarning(
					$"BehaviourDocs: no XML doc file found. Searched: {string.Join(", ", CandidateXmlPaths)}. " +
					"Confirm Assets/Behaviours/csc.rsp includes `-doc:Temp/Assembler.Behaviours.xml` and the assembly has compiled.");
				return result;
			}

			foreach (var m in doc.Descendants("member"))
			{
				var nameAttr = m.Attribute("name")?.Value;
				if (!string.IsNullOrEmpty(nameAttr))
				{
					result[nameAttr!] = m;
				}
			}

			return result;
		}

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
				var key = "T:" + XmlDocTypeName(t);
				if (membersByKey.TryGetValue(key, out var member))
				{
					yield return member;
				}
			}
		}

		// Approximates the type-name encoding used by C# XML doc files:
		//   - generic types end with `Arity (e.g. VariableSetterBehaviour`1)
		//   - nested types use '+' replaced by '.'
		private static string XmlDocTypeName(Type t)
		{
			var name = (t.Namespace is null ? "" : t.Namespace + ".") + t.Name;
			return name.Replace('+', '.');
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
			Type? monoType,
			IReadOnlyList<InfoProp> infoProps,
			Dictionary<string, PropDoc> propsDocs,
			List<string> warnings)
		{
			var infoNames = new HashSet<string>(infoProps.Select(p => p.YamlName));
			var docNames = new HashSet<string>(propsDocs.Keys);
			var docSource = monoType?.Name ?? $"hand-authored doc for `{behaviourName}`";

			foreach (var missing in infoNames.Except(docNames))
			{
				warnings.Add(
					$"`{behaviourName}`: property `{missing}` on `{infoType.Name}` is missing from {docSource}'s `Properties:` block.");
			}

			foreach (var extra in docNames.Except(infoNames))
			{
				warnings.Add(
					$"`{behaviourName}`: {docSource} documents `{extra}` in its `Properties:` block but `{infoType.Name}` has no such property.");
			}
		}

		// ----------------------------------------------------------------------
		// Hand-authored docs (fallback for behaviours that don't have a single
		// MonoBehaviour to attach XML comments to — e.g. listener fan-in nodes
		// like `when all` / `when any` or pure logic gates like `condition`).
		//
		// Property names here must match the YAML keys that the corresponding
		// Info record exposes (i.e. the record's primary-ctor parameter names,
		// or the `[YamlName(...)]` override). Doc-gen still reflects the Info
		// for the property *type*; this map only supplies the summary and the
		// per-property/per-output human descriptions.
		// ----------------------------------------------------------------------

		private sealed record HandAuthoredDoc(
			string Summary,
			IReadOnlyDictionary<string, PropDoc> Properties);

		private readonly static IReadOnlyDictionary<string, HandAuthoredDoc> HandAuthoredDocs =
			new Dictionary<string, HandAuthoredDoc>
			{
				["condition"] = new HandAuthoredDoc(
					"Evaluates an expression against the supplied Arguments and forwards to listeners only when the result is true. Use this when a gate's condition is itself a reusable expression (declared elsewhere by `ExpressionId`) rather than an inline boolean — for inline boolean conditions, prefer `condition gate`.",
					new Dictionary<string, PropDoc>
					{
						["ExpressionId"] = new(null, "Reference to a compiled boolean expression (declared in the `expressions:` section). The expression is evaluated each time an upstream listener fires this behaviour."),
						["Arguments"] = new("object[]", "Positional arguments passed to the referenced expression. Each entry may be a constant, variable reference, or another expression result."),
					}),
				["when all"] = new HandAuthoredDoc(
					"Listener fan-in: fires its listeners only after **every** trigger named in `TriggerIds` has fired at least once. Useful for \"do X once all of A, B and C have happened\" gating. Each `TriggerIds` entry is the `id` of another trigger (or trigger-like) behaviour defined on the same entity or game.",
					new Dictionary<string, PropDoc>
					{
						["TriggerIds"] = new("string[]", "Ids of upstream triggers to wait on. Listeners fire once after the last id in the set has been observed."),
					}),
				["when any"] = new HandAuthoredDoc(
					"Listener fan-in: fires its listeners as soon as **any** trigger named in `TriggerIds` fires. Equivalent to subscribing the same listener to every id in the set, but expressed as a single named node so multiple downstream behaviours can share it.",
					new Dictionary<string, PropDoc>
					{
						["TriggerIds"] = new("string[]", "Ids of upstream triggers to listen to. Listeners fire whenever any one of them fires."),
					}),
			};

		// ----------------------------------------------------------------------
		// Type rendering
		// ----------------------------------------------------------------------

		private readonly static Dictionary<Type, string> PrimitiveNames = new()
		{
			[typeof(bool)] = "bool",
			[typeof(byte)] = "byte",
			[typeof(int)] = "int",
			[typeof(long)] = "long",
			[typeof(float)] = "float",
			[typeof(double)] = "double",
			[typeof(string)] = "string",
			[typeof(object)] = "object",
		};

		private static string RenderType(Type t)
		{
			if (PrimitiveNames.TryGetValue(t, out var simple))
			{
				return simple;
			}

			if (t.IsArray)
			{
				return RenderType(t.GetElementType()!) + "[]";
			}

			if (t.IsGenericType)
			{
				var defName = t.Name;
				var tickIndex = defName.IndexOf('`');
				if (tickIndex >= 0)
				{
					defName = defName.Substring(0, tickIndex);
				}

				var args = string.Join(", ", t.GetGenericArguments().Select(RenderType));
				return $"{defName}<{args}>";
			}

			return t.Name;
		}
	}
}
