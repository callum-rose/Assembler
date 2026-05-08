using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Assembler.Generators;

internal static class TypeHierarchyHelper
{
	public static IReadOnlyList<(string trimmedName, string fullName, INamedTypeSymbol typeSymbol)> GetDerivedTypes(
		INamedTypeSymbol baseSymbol,
		bool includeAbstractClasses,
		Compilation compilation)
	{
		var derivedTypes = new List<(string fullName, INamedTypeSymbol typeSymbol)>();

		foreach (var syntaxTree in compilation.SyntaxTrees)
		{
			var semanticModel = compilation.GetSemanticModel(syntaxTree);
			var root = syntaxTree.GetRoot();

			var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

			foreach (var classDeclaration in classDeclarations)
			{
				if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol typeSymbol)
				{
					continue;
				}

				if (InheritsFrom(typeSymbol, baseSymbol) && typeSymbol.TypeKind == TypeKind.Class)
				{
					if (!includeAbstractClasses && typeSymbol.IsAbstract)
					{
						continue;
					}

					var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
						.Replace("global::", "")
						.Replace(".", "");
					derivedTypes.Add((fullName, typeSymbol));
				}
			}
		}

		return TrimCommonPrefix(derivedTypes);
	}

	private static bool InheritsFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseSymbol)
	{
		var current = typeSymbol.BaseType;

		while (current != null)
		{
			if (SymbolEqualityComparer.Default.Equals(current, baseSymbol))
			{
				return true;
			}

			current = current.BaseType;
		}

		return false;
	}

	private static List<(string trimmedName, string fullName, INamedTypeSymbol typeSymbol)> TrimCommonPrefix(
		List<(string fullName, INamedTypeSymbol typeSymbol)> types)
	{
		if (types.Count < 2)
		{
			return types.Select(t => (t.fullName, t.fullName, t.typeSymbol)).ToList();
		}

		var firstParts = types[0].fullName.Split('_');
		var commonPrefixLength = 0;

		for (int i = 0; i < firstParts.Length - 1; i++)
		{
			var part = firstParts[i];

			if (types.All(t => t.fullName.Split('_').Length > i && t.fullName.Split('_')[i] == part))
			{
				commonPrefixLength = i + 1;
			}
			else
			{
				break;
			}
		}

		if (commonPrefixLength > 0)
		{
			return types.Select(t =>
			{
				var parts = t.fullName.Split('_');
				var trimmedName = string.Join("_", parts.Skip(commonPrefixLength));
				return (trimmedName, t.fullName, t.typeSymbol);
			}).ToList();
		}

		return types.Select(t => (t.fullName, t.fullName, t.typeSymbol)).ToList();
	}
}