using Microsoft.CodeAnalysis;

namespace NoahStolk.StrictEnums.SourceGen.Extensions;

public static class SyntaxNodeExtensions
{
	public static string? GetFullTypeName(this SyntaxNode syntaxNode, Compilation compilation)
	{
		SemanticModel semanticModel = compilation.GetSemanticModel(syntaxNode.SyntaxTree);
		if (semanticModel.GetDeclaredSymbol(syntaxNode) is not INamedTypeSymbol symbol)
			return null;

		return symbol.ToString();
	}
}
