using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZBase.Foundation.SourceGen;

namespace ZBase.Foundation.PolymorphicStructs.PolymorphicStructSourceGen
{
    [Generator]
    public class PolymorphicStructGenerator : IIncrementalGenerator
    {
        public const string GENERATOR_NAME = nameof(PolymorphicStructGenerator);
        public const string POLY_INTERFACE_ATTRIBUTE = "global::ZBase.Foundation.PolymorphicStructs.PolymorphicStructInterfaceAttribute";
        public const string POLY_STRUCT_ATTRIBUTE = "global::ZBase.Foundation.PolymorphicStructs.PolymorphicStructAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var projectPathProvider = SourceGenHelpers.GetSourceGenConfigProvider(context);

            var interfaceRefProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: IsValidInterfaceSyntax,
                transform: GetInterfaceRefSemanticMatch
            ).Where(static t => t is { });

            var structRefProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: IsValidStructSyntax,
                transform: GetStructRefSemanticMatch
            ).Where(static t => t is { });

            var combined = interfaceRefProvider.Collect()
                .Combine(structRefProvider.Collect())
                .Combine(context.CompilationProvider)
                .Combine(projectPathProvider);

            context.RegisterSourceOutput(combined, static (sourceProductionContext, source) => {
                GenerateOutput(
                    sourceProductionContext
                    , source.Left.Right
                    , source.Left.Left.Left
                    , source.Left.Left.Right
                    , source.Right.projectPath
                    , source.Right.outputSourceGenFiles
                );
            });
        }

        private static bool IsValidInterfaceSyntax(SyntaxNode node, CancellationToken _)
        {
            return node is InterfaceDeclarationSyntax syntax
                && syntax.AttributeLists.Count > 0
                && syntax.HasAttributeCandidate("ZBase.Foundation.PolymorphicStructs", "PolymorphicStructInterface");
        }

        public static INamedTypeSymbol GetInterfaceRefSemanticMatch(
              GeneratorSyntaxContext context
            , CancellationToken token
        )
        {
            if (context.SemanticModel.Compilation.IsValidCompilation() == false
                || context.Node is not InterfaceDeclarationSyntax syntax
            )
            {
                return null;
            }

            var semanticModel = context.SemanticModel;
            var symbol = semanticModel.GetDeclaredSymbol(syntax, token);

            if (symbol.HasAttribute(POLY_INTERFACE_ATTRIBUTE))
            {
                return symbol;
            }

            return null;
        }

        private static bool IsValidStructSyntax(SyntaxNode node, CancellationToken _)
        {
            return node is StructDeclarationSyntax syntax
                && syntax.AttributeLists.Count > 0
                && syntax.HasAttributeCandidate("ZBase.Foundation.PolymorphicStructs", "PolymorphicStruct")
                && syntax.BaseList != null
                && syntax.BaseList.Types.Count > 0
                && syntax.BaseList.Types.Any(x => x.HasAttributeCandidate("ZBase.Foundation.PolymorphicStructs", "PolymorphicStructInterface"))
                ;
        }

        public static INamedTypeSymbol GetStructRefSemanticMatch(
              GeneratorSyntaxContext context
            , CancellationToken token
        )
        {
            if (context.SemanticModel.Compilation.IsValidCompilation() == false
                || context.Node is not StructDeclarationSyntax syntax
                || syntax.BaseList == null
                || syntax.BaseList.Types.Count < 1
            )
            {
                return null;
            }

            var semanticModel = context.SemanticModel;
            var symbol = semanticModel.GetDeclaredSymbol(syntax, token);

            if (symbol.HasAttribute(POLY_STRUCT_ATTRIBUTE) == false)
            {
                return null;
            }

            foreach (var item in symbol.Interfaces)
            {
                if (item.HasAttribute(POLY_INTERFACE_ATTRIBUTE))
                {
                    return symbol;
                }
            }

            return null;
        }

        private static void GenerateOutput(
              SourceProductionContext context
            , Compilation compilation
            , ImmutableArray<INamedTypeSymbol> interfaces
            , ImmutableArray<INamedTypeSymbol> structs
            , string projectPath
            , bool outputSourceGenFiles
        )
        {
            if (interfaces.Length < 1)
            {
                return;
            }

            context.CancellationToken.ThrowIfCancellationRequested();

            var interfaceSet = interfaces.ToImmutableHashSet(SymbolEqualityComparer.Default);
            var structMap = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
            
            foreach (var item in structs)
            {
                var baseType = item.BaseType;

                while (baseType != null)
                {
                    if (interfaceSet.Contains(baseType))
                    {
                        if (structMap.TryGetValue(baseType, out var set) == false)
                        {
                            set = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                            structMap[baseType] = set;
                        }

                        set.Add(item);
                    }

                    baseType = baseType.BaseType;
                }
            }


        }

        private static readonly DiagnosticDescriptor s_errorDescriptor
            = new("SG_POLYMORPHIC_STRUCT_01"
                , "Polymorphic Struct Generator Error"
                , "This error indicates a bug in the Polymorphic Struct source generators. Error message: '{0}'."
                , "ZBase.Foundation.PolymorphicStructs.PolymorphicStructSourceGen.PolymorphicStructGenerator"
                , DiagnosticSeverity.Error
                , isEnabledByDefault: true
                , description: ""
            );
    }
}