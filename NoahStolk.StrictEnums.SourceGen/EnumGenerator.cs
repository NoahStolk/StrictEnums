using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Linq;
using System;
using NoahStolk.StrictEnums.SourceGen.Utils;
using NoahStolk.StrictEnums.SourceGen.Extensions;

namespace NoahStolk.StrictEnums.SourceGen;

[Generator]
public class EnumGenerator : IIncrementalGenerator
{
	private const string _namespace = $"%{nameof(_namespace)}%";
	private const string _className = $"%{nameof(_className)}%";
	private const string _valueTypeName = $"%{nameof(_valueTypeName)}%";
	private const string _convertFrom = $"%{nameof(_convertFrom)}%";
	private const string _toString = $"%{nameof(_toString)}%";

	private const string _enumTemplate = $$"""
		using System;

		namespace {{_namespace}};
		
		public partial class {{_className}}
		{
			private {{_className}}({{_valueTypeName}} value)
			{
				Value = value;
			}

			public {{_valueTypeName}} Value { get; }

			public static {{_className}} ConvertFrom({{_valueTypeName}} value) => value switch
			{
				{{_convertFrom}}
				_ => throw new InvalidOperationException($"Impossible value {value} for {nameof({{_className}})}."),
			};

			public override string ToString() => Value switch
			{
				{{_toString}}
				_ => throw new InvalidOperationException($"Impossible value {Value} for {nameof({{_className}})}."),
			};
		}
		""";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		IncrementalValuesProvider<ClassDeclarationSyntax> enumTypeDeclarations = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (sn, _) => sn is ClassDeclarationSyntax cds && cds.BaseList?.Types.Any(bts => bts.ToString().StartsWith("IStrictEnum")) == true, // TODO: Find more robust way to check for correct interface.
				transform: static (ctx, _) => ctx.Node as ClassDeclarationSyntax)
			.Where(static m => m is not null)!;

		IncrementalValueProvider<(Compilation Compilation, ImmutableArray<ClassDeclarationSyntax> EnumTypeDeclarations)> compilation = context.CompilationProvider.Combine(enumTypeDeclarations.Collect());

		context.RegisterSourceOutput(compilation, static (spc, source) => Execute(source.Compilation, source.EnumTypeDeclarations, spc));

		static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> enumTypeDeclarations, SourceProductionContext context)
		{
			if (enumTypeDeclarations.IsDefaultOrEmpty)
				return;

			foreach (EnumData enumData in GetEnumData(compilation, enumTypeDeclarations.Distinct(), context.CancellationToken))
			{
				string sourceBuilder = _enumTemplate
					.Replace(_namespace, enumData.Namespace)
					.Replace(_className, enumData.ClassName)
					.Replace(_valueTypeName, enumData.ValueTypeName)
					.Replace(_convertFrom, string.Join(SourceBuilderUtils.NewLine, enumData.Values.Select(kvp => $"{kvp.Value} => {kvp.Key},")).IndentCode(2))
					.Replace(_toString, string.Join(SourceBuilderUtils.NewLine, enumData.Values.Select(kvp => $"{kvp.Value} => nameof({kvp.Key}),")).IndentCode(2));

				context.AddSource($"{enumData.ClassName}.g.cs", SourceBuilderUtils.Build(sourceBuilder));
			}
		}
	}

	private static List<EnumData> GetEnumData(Compilation compilation, IEnumerable<ClassDeclarationSyntax> enumTypeDeclarations, CancellationToken cancellationToken)
	{
		List<EnumData> enums = new();
		foreach (ClassDeclarationSyntax cds in enumTypeDeclarations)
		{
			cancellationToken.ThrowIfCancellationRequested();

			string? fullTypeName = cds.GetFullTypeName(compilation);
			if (fullTypeName == null)
				continue;

			Dictionary<string, string> values = new();
			foreach (FieldDeclarationSyntax fds in cds.ChildNodes().Where(sn => sn is FieldDeclarationSyntax).Cast<FieldDeclarationSyntax>())
			{
				foreach (VariableDeclaratorSyntax variable in fds.Declaration.Variables)
				{
					string key = variable.Identifier.ValueText;
					foreach (SyntaxNode variableChild in variable.ChildNodes())
					{
						if (values.ContainsKey(key))
							break;

						if (variableChild is not EqualsValueClauseSyntax equals)
							continue;

						if (equals.Value is not ImplicitObjectCreationExpressionSyntax expressionSyntax)
							continue;

						if (expressionSyntax.ArgumentList.Arguments.Count == 0)
							continue;

						ArgumentSyntax argument = expressionSyntax.ArgumentList.Arguments[0];
						values.Add(variable.Identifier.ValueText, argument.ToString());
					}
				}
			}

			enums.Add(new(fullTypeName, "int", values)); // TODO
		}

		return enums;
	}

	private sealed class EnumData
	{
		public EnumData(string fullTypeName, string valueTypeName, Dictionary<string, string> values)
		{
			int separatorIndex = fullTypeName.LastIndexOf('.');
			if (separatorIndex == -1)
				throw new InvalidOperationException($"Invalid full type name, at least one '.' character was expected: {fullTypeName}");

			Namespace = fullTypeName.Substring(0, separatorIndex);
			ClassName = fullTypeName.Substring(separatorIndex + 1, fullTypeName.Length - (separatorIndex + 1));
			ValueTypeName = valueTypeName;
			Values = values;
		}

		public string Namespace { get; }

		public string ClassName { get; }

		public string ValueTypeName { get; }

		public Dictionary<string, string> Values { get; }
	}
}
