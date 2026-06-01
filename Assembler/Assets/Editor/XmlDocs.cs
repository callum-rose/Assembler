using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace Editor
{
	// Shared plumbing for the XML-doc-driven documentation generators (BehaviourDocs,
	// LibraryDocs). Loads a compiler-emitted `-doc:` XML file into a member lookup and
	// renders CLR types / XML-doc member ids.
	public static class XmlDocs
	{
		// ----------------------------------------------------------------------
		// XML doc loading
		// ----------------------------------------------------------------------

		// Loads the first existing XML doc file from the candidate paths and returns its
		// <member> elements keyed by their `name` attribute (e.g. "T:Foo.Bar",
		// "M:Foo.Bar.Baz(System.Single)"). Returns an empty dictionary (and warns) if none exist.
		public static Dictionary<string, XElement> LoadMembers(IReadOnlyList<string> candidateXmlPaths)
		{
			var result = new Dictionary<string, XElement>();
			XDocument? doc = null;
			foreach (var p in candidateXmlPaths)
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
					$"XmlDocs: no XML doc file found. Searched: {string.Join(", ", candidateXmlPaths)}. " +
					"Confirm the assembly's csc.rsp includes a `-doc:` flag and the assembly has compiled.");
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

		// ----------------------------------------------------------------------
		// Mixed-content flattening
		// ----------------------------------------------------------------------

		// Flattens an XML doc element's mixed content to plain text, resolving cross-reference
		// elements to readable names. Plain XElement.Value silently DROPS the content of
		// self-closing tags like <see cref="Foo"/>, which leaves dangling "See ." fragments in the
		// generated markdown. Here <see>/<seealso> render their inner text (or the cref's short
		// name) and <paramref>/<typeparamref> render their name. Use this instead of `.Value` for
		// any summary/param/returns text that may contain such references.
		public static string Flatten(XElement element)
		{
			var sb = new StringBuilder();
			AppendNodes(element, sb);
			return sb.ToString();
		}

		private static void AppendNodes(XElement element, StringBuilder sb)
		{
			foreach (var node in element.Nodes())
			{
				switch (node)
				{
					case XText text:
						sb.Append(text.Value);
						break;
					case XElement child:
						AppendElement(child, sb);
						break;
				}
			}
		}

		private static void AppendElement(XElement el, StringBuilder sb)
		{
			switch (el.Name.LocalName)
			{
				case "see":
				case "seealso":
					var inner = el.Value;
					sb.Append(!string.IsNullOrWhiteSpace(inner) ? inner : CrefName(el.Attribute("cref")?.Value));
					break;
				case "paramref":
				case "typeparamref":
					sb.Append(el.Attribute("name")?.Value ?? "");
					break;
				default:
					// Unknown wrapper (e.g. <para>, <c>): keep its text content.
					AppendNodes(el, sb);
					break;
			}
		}

		// "T:Some.Namespace.ListClearBehaviour`1" -> "ListClearBehaviour"
		private static string CrefName(string? cref)
		{
			if (string.IsNullOrEmpty(cref))
			{
				return "";
			}

			var s = cref!;
			var colon = s.IndexOf(':');
			if (colon >= 0)
			{
				s = s.Substring(colon + 1);
			}

			var tick = s.IndexOf('`');
			if (tick >= 0)
			{
				s = s.Substring(0, tick);
			}

			var dot = s.LastIndexOf('.');
			if (dot >= 0)
			{
				s = s.Substring(dot + 1);
			}

			return s;
		}

		// ----------------------------------------------------------------------
		// XML-doc member ids
		// ----------------------------------------------------------------------

		// Approximates the type-name encoding used by C# XML doc files:
		//   - generic types end with `Arity (e.g. VariableSetterBehaviour`1)
		//   - nested types use '+' replaced by '.'
		public static string XmlDocTypeName(Type t)
		{
			var name = (t.Namespace is null ? "" : t.Namespace + ".") + t.Name;
			return name.Replace('+', '.');
		}

		// Builds the XML-doc member id for a method, e.g.
		//   M:Assembler.Libraries.GridMath.CellToWorld(UnityEngine.Vector3,System.Single,System.Single)
		// matching the `name` attribute the compiler writes so a specific overload's
		// <summary>/<param>/<returns> can be looked up.
		public static string MethodMemberId(MethodInfo m)
		{
			var sb = new StringBuilder();
			sb.Append("M:").Append(XmlDocTypeName(m.DeclaringType!)).Append('.').Append(m.Name);

			var parameters = m.GetParameters();
			if (parameters.Length > 0)
			{
				sb.Append('(');
				sb.Append(string.Join(",", parameters.Select(p => DocParamTypeName(p.ParameterType))));
				sb.Append(')');
			}

			return sb.ToString();
		}

		// Encodes a parameter type the way the XML-doc id does: full names, nested '+' -> '.',
		// generic args in curly braces (List{UnityEngine.Vector3}), arrays as [], by-ref as @.
		private static string DocParamTypeName(Type t)
		{
			if (t.IsByRef)
			{
				return DocParamTypeName(t.GetElementType()!) + "@";
			}

			if (t.IsArray)
			{
				return DocParamTypeName(t.GetElementType()!) + "[]";
			}

			if (t.IsGenericType)
			{
				var name = t.GetGenericTypeDefinition().FullName ?? t.GetGenericTypeDefinition().Name;
				var tick = name.IndexOf('`');
				if (tick >= 0)
				{
					name = name.Substring(0, tick);
				}

				name = name.Replace('+', '.');
				var args = string.Join(",", t.GetGenericArguments().Select(DocParamTypeName));
				return $"{name}{{{args}}}";
			}

			return (t.FullName ?? t.Name).Replace('+', '.');
		}

		// ----------------------------------------------------------------------
		// Type rendering (human-readable, for the markdown tables)
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
			[typeof(void)] = "void",
		};

		public static string RenderType(Type t)
		{
			if (PrimitiveNames.TryGetValue(t, out var simple))
			{
				return simple;
			}

			if (t.IsByRef)
			{
				return RenderType(t.GetElementType()!);
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
