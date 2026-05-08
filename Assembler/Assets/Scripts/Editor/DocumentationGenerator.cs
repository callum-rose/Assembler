// #if UNITY_EDITOR
// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Reflection;
// using System.Text;
// using Core;
// using UnityEditor;
// using UnityEditor.Compilation;
// using UnityEngine;
//
// namespace Editor
// {
// 	[InitializeOnLoad]
// 	public static class DocumentationGenerator
// 	{
// 		static DocumentationGenerator()
// 		{
// 			CompilationPipeline.compilationFinished += OnCompilationFinished;
// 		}
//
// 		private static void OnCompilationFinished(object obj)
// 		{
// 			EditorApplication.delayCall += GenerateDocumentation;
// 		}
//
// 		[MenuItem("Tools/Generate Documentation")]
// 		public static void GenerateDocumentation()
// 		{
// 			try
// 			{
// 				var documentationBuilder = new StringBuilder();
// 				documentationBuilder.AppendLine("# API Documentation");
// 				documentationBuilder.AppendLine();
//
// 				var assemblies = AppDomain.CurrentDomain.GetAssemblies()
// 					.Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location));
//
// 				foreach (var type in assemblies
// 					         .SelectMany(a => a.GetTypes())
// 					         .Where(t => t.GetCustomAttribute<GenerateDocumentationAttribute>() is not null))
// 				{
// 					GenerateMarkdownDocumentation(type, documentationBuilder);
// 				}
//
// 				var outputPath = Path.Combine(Application.dataPath, "docs");
// 				var outputFile = Path.Combine(outputPath, "Documentation.md");
// 				Directory.CreateDirectory(outputPath);
// 				File.WriteAllText(outputFile, documentationBuilder.ToString());
//
// 				Debug.Log($"Documentation generated: {outputFile}");
// 				AssetDatabase.Refresh();
// 			}
// 			catch (Exception ex)
// 			{
// 				Debug.LogError("Documentation generation failed");
// 				Debug.LogException(ex);
// 			}
// 		}
//
// 		private static void GenerateMarkdownDocumentation(Type type, StringBuilder sb)
// 		{
// 			var concreteTypes = GetAllConcreteDescendants(type);
//
// 			var typesByNamespace = concreteTypes
// 				.GroupBy(t => t.Namespace ?? "Global")
// 				.OrderBy(g => g.Key);
//
// 			foreach (var namespaceGroup in typesByNamespace)
// 			{
// 				sb.AppendLine($"## {namespaceGroup.Key}");
// 				sb.AppendLine();
//
// 				foreach (var concreteType in namespaceGroup.OrderBy(t => t.Name))
// 				{
// 					sb.AppendLine($"### {concreteType.Name}");
// 					sb.AppendLine();
//
// 					var injectableMembers = GetInjectableMembers(concreteType);
//
// 					if (injectableMembers.Count > 0)
// 					{
// 						foreach (var (attr, memberType) in injectableMembers)
// 						{
// 							if (attr is not null)
// 							{
// 								var name = GetAttributeValue(attr, "Name");
// 								var description = GetAttributeValue(attr, "Description");
// 								var typeString = GetNiceTypeName(memberType);
//
// 								sb.Append($"- {name} `{typeString}`");
//
// 								if (!string.IsNullOrEmpty(description))
// 								{
// 									sb.Append($": {description}");
// 								}
//
// 								sb.AppendLine();
// 							}
// 						}
//
// 						sb.AppendLine();
// 					}
// 				}
// 			}
// 		}
//
// 		private static string GetNiceTypeName(Type type)
// 		{
// 			if (type == typeof(int))
// 			{
// 				return "int";
// 			}
//
// 			if (type == typeof(float))
// 			{
// 				return "float";
// 			}
//
// 			if (type == typeof(bool))
// 			{
// 				return "bool";
// 			}
//
// 			if (type == typeof(string))
// 			{
// 				return "string";
// 			}
//
// 			if (type.IsArray)
// 			{
// 				return $"{GetNiceTypeName(type.GetElementType())}[]";
// 			}
//
// 			if (type.IsGenericType)
// 			{
// 				string typeArgs = string.Join(", ", type.GenericTypeArguments.Select(GetNiceTypeName));
// 				return $"{type.Name.Split('`')[0]}<{typeArgs}>";
// 			}
//
// 			return type.Name;
// 		}
//
// 		private static IReadOnlyList<Type> GetAllConcreteDescendants(Type baseType) =>
// 			AppDomain.CurrentDomain.GetAssemblies()
// 				.Where(a => !a.IsDynamic)
// 				.SelectMany(a => a.GetTypes())
// 				.Where(t => !t.IsAbstract && t.IsSubclassOf(baseType))
// 				.ToArray();
//
// 		private static IReadOnlyList<(InjectAttribute injectAttribute, Type memberType)>
// 			GetInjectableMembers(Type type)
// 		{
// 			var members = new List<(InjectAttribute, Type)>();
// 			var currentType = type;
// 			var derivedType = type;
//
// 			while (currentType != null && currentType != typeof(object))
// 			{
// 				var typeMembers = currentType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
// 				                                         BindingFlags.Instance | BindingFlags.DeclaredOnly)
// 					.Where(m => m is FieldInfo or PropertyInfo)
// 					.Where(m => m.GetCustomAttribute<InjectAttribute>() is not null)
// 					.Select(m => (
// 							m.GetCustomAttribute<InjectAttribute>()!,
// 							ResolveMemberType(m, currentType, derivedType)
// 						));
//
// 				members.AddRange(typeMembers);
// 				currentType = currentType.BaseType;
// 			}
//
// 			return members;
// 		}
//
// 		private static Type ResolveMemberType(MemberInfo member, Type declaringType, Type derivedType)
// 		{
// 			var memberType = member switch
// 			{
// 				FieldInfo fieldInfo => fieldInfo.FieldType,
// 				PropertyInfo propertyInfo => propertyInfo.PropertyType,
// 				_ => throw new ArgumentOutOfRangeException(nameof(member), member, null)
// 			};
//
// 			// If the member type is a generic type parameter, resolve it using the derived type
// 			if (memberType.IsGenericParameter)
// 			{
// 				var genericArgs = derivedType.BaseType?.GetGenericArguments();
// 				var parameterPosition = memberType.GenericParameterPosition;
//
// 				if (genericArgs != null && parameterPosition < genericArgs.Length)
// 				{
// 					return genericArgs[parameterPosition];
// 				}
// 			}
//
// 			// If the member type contains generic parameters, substitute them
// 			if (memberType.ContainsGenericParameters && declaringType.IsGenericType)
// 			{
// 				var genericTypeDefinition = declaringType.GetGenericTypeDefinition();
// 				var genericArgs = derivedType.BaseType?.GetGenericArguments();
//
// 				if (genericArgs != null)
// 				{
// 					return SubstituteGenericArguments(memberType,
// 						genericTypeDefinition.GetGenericArguments(),
// 						genericArgs);
// 				}
// 			}
//
// 			return memberType;
// 		}
//
// 		private static Type SubstituteGenericArguments(Type type, Type[] typeParameters, Type[] typeArguments)
// 		{
// 			if (type.IsGenericParameter)
// 			{
// 				var index = Array.IndexOf(typeParameters, type);
// 				return index >= 0 && index < typeArguments.Length ? typeArguments[index] : type;
// 			}
//
// 			if (type.IsGenericType)
// 			{
// 				var genericArgs = type.GetGenericArguments()
// 					.Select(arg => SubstituteGenericArguments(arg, typeParameters, typeArguments))
// 					.ToArray();
//
// 				return type.GetGenericTypeDefinition().MakeGenericType(genericArgs);
// 			}
//
// 			return type;
// 		}
//
// 		private static string GetAttributeValue(Attribute attribute, string propertyName)
// 		{
// 			return attribute.GetType().GetProperty(propertyName)?.GetValue(attribute)?.ToString() ?? string.Empty;
// 		}
// 	}
// }
// #endif