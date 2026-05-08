using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

public static class MemberHelper
{
	public static IReadOnlyList<string> GetMembers(INamedTypeSymbol classSymbol, ITypeSymbol filterType)
	{
		if (filterType is not INamedTypeSymbol targetType)
		{
			return [];
		}

		return classSymbol
			.GetMembers()
			.OfType<IFieldSymbol>()
			.Where(f => f.IsStatic &&
			            f.IsReadOnly &&
			            f.DeclaredAccessibility == Accessibility.Public &&
			            f.Type is INamedTypeSymbol fieldType &&
			            SymbolEqualityComparer.Default.Equals(fieldType.ConstructedFrom, targetType.ConstructedFrom) &&
			            fieldType.TypeArguments.Length == targetType.TypeArguments.Length &&
			            fieldType.TypeArguments
				            .Zip(targetType.TypeArguments, (a, b) => SymbolEqualityComparer.Default.Equals(a, b))
				            .All(equal => equal))
			.Select(f => f.Name)
			.ToList();
	}
}