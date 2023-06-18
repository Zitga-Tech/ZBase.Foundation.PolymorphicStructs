using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZBase.Foundation.SourceGen;

namespace ZBase.Foundation.PolymorphicStructs.PolymorphicStructSourceGen
{
    using StructMap = Dictionary<INamedTypeSymbol, StructRef>;
    using InterfaceMap = Dictionary<ISymbol, InterfaceRef>;
    using InterfaceToStructMap = Dictionary<INamedTypeSymbol, Dictionary<INamedTypeSymbol, StructRef>>;

    [Generator]
    public partial class PolymorphicStructGenerator : IIncrementalGenerator
    {
        public const string GENERATOR_NAME = nameof(PolymorphicStructGenerator);
        public const string POLY_INTERFACE_ATTRIBUTE = "global::ZBase.Foundation.PolymorphicStructs.PolymorphicStructInterfaceAttribute";
        public const string POLY_STRUCT_ATTRIBUTE = "global::ZBase.Foundation.PolymorphicStructs.PolymorphicStructAttribute";

        private const string AGGRESSIVE_INLINING = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";
        private const string GENERATED_CODE = "[global::System.CodeDom.Compiler.GeneratedCode(\"ZBase.Foundation.PolymorphicStructs.PolymorphicStructSourceGen.PolymorphicStructGenerator\", \"1.0.0\")]";
        private const string EXCLUDE_COVERAGE = "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var projectPathProvider = SourceGenHelpers.GetSourceGenConfigProvider(context);

            var interfaceRefProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: IsValidInterfaceSyntax,
                transform: GetInterfaceRefSemanticMatch
            ).Where(static t => t.syntax is { } && t.symbol is { });

            var structRefProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: IsValidStructSyntax,
                transform: GetStructRefSemanticMatch
            ).Where(static t => t.syntax is { } && t.symbol is { });

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

        public static (InterfaceDeclarationSyntax syntax, INamedTypeSymbol symbol) GetInterfaceRefSemanticMatch(
              GeneratorSyntaxContext context
            , CancellationToken token
        )
        {
            if (context.SemanticModel.Compilation.IsValidCompilation() == false
                || context.Node is not InterfaceDeclarationSyntax syntax
            )
            {
                return (null, null);
            }

            var semanticModel = context.SemanticModel;
            var symbol = semanticModel.GetDeclaredSymbol(syntax, token);

            if (symbol.HasAttribute(POLY_INTERFACE_ATTRIBUTE))
            {
                return (syntax, symbol);
            }

            return (null, null);
        }

        private static bool IsValidStructSyntax(SyntaxNode node, CancellationToken _)
        {
            return node is StructDeclarationSyntax syntax
                && syntax.AttributeLists.Count > 0
                && syntax.HasAttributeCandidate("ZBase.Foundation.PolymorphicStructs", "PolymorphicStruct")
                && syntax.BaseList != null
                && syntax.BaseList.Types.Count > 0
                ;
        }

        public static (StructDeclarationSyntax syntax, INamedTypeSymbol symbol) GetStructRefSemanticMatch(
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
                return (null, null);
            }

            var semanticModel = context.SemanticModel;
            var symbol = semanticModel.GetDeclaredSymbol(syntax, token);

            if (symbol.HasAttribute(POLY_STRUCT_ATTRIBUTE) == false)
            {
                return (null, null);
            }

            foreach (var item in symbol.AllInterfaces)
            {
                if (item.HasAttribute(POLY_INTERFACE_ATTRIBUTE))
                {
                    return (syntax, symbol);
                }
            }

            return (null, null);
        }

        private static void GenerateOutput(
              SourceProductionContext context
            , Compilation compilation
            , ImmutableArray<(InterfaceDeclarationSyntax syntax, INamedTypeSymbol symbol)> interfaces
            , ImmutableArray<(StructDeclarationSyntax syntax, INamedTypeSymbol symbol)> structs
            , string projectPath
            , bool outputSourceGenFiles
        )
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            SourceGenHelpers.ProjectPath = projectPath;

            BuildMaps(
                  context
                , interfaces
                , structs
                , out var interfaceMap
                , out var interfaceToStructMap
                , out var structRefs
                , out var count
            );

            var mergedFieldRefPool = new Queue<MergedFieldRef>(count);
            var mergedFieldRefList = new List<MergedFieldRef>(count);
            var sb = new StringBuilder();

            foreach (var kv in interfaceToStructMap)
            {
                BuildMergedFieldRefList(kv.Value, mergedFieldRefList, mergedFieldRefPool);

                if (interfaceMap.TryGetValue(kv.Key, out var interfaceRef))
                {
                    interfaceMap.Remove(kv.Key);

                    GenerateMergedStruct(
                          context
                        , compilation
                        , outputSourceGenFiles
                        , interfaceRef
                        , kv.Value.Values
                        , (ulong)kv.Value.Count
                        , mergedFieldRefList
                        , sb
                    );
                }

                ClearToPool(mergedFieldRefList, mergedFieldRefPool);
            }

            GenerateStructs(
                  context
                , compilation
                , outputSourceGenFiles
                , structRefs
            );

            GenerateEmptyMergedStruct(
                  context
                , compilation
                , outputSourceGenFiles
                , interfaceMap.Values
                , sb
            );
        }

        private static void BuildMaps(
              SourceProductionContext context
            , ImmutableArray<(InterfaceDeclarationSyntax syntax, INamedTypeSymbol symbol)> interfaces
            , ImmutableArray<(StructDeclarationSyntax syntax, INamedTypeSymbol symbol)> structs
            , out InterfaceMap interfaceMap
            , out InterfaceToStructMap interfaceToStructMap
            , out ImmutableArray<StructRef> structRefs
            , out int maxStructCount
        )
        {
            interfaceMap = new(SymbolEqualityComparer.Default);

            foreach (var (syntax, symbol) in interfaces)
            {
                if (interfaceMap.ContainsKey(symbol) == false)
                {
                    interfaceMap.Add(symbol, new InterfaceRef(syntax, symbol));
                }
            }

            interfaceToStructMap = new InterfaceToStructMap(
                SymbolEqualityComparer.Default
            );

            maxStructCount = 0;

            using var structRefArrayBuilder = ImmutableArrayBuilder<StructRef>.Rent();

            foreach (var (syntax, symbol) in structs)
            {
                StructRef structRef = null;

                foreach (var @interface in symbol.AllInterfaces)
                {
                    if (interfaceMap.TryGetValue(@interface, out var interfaceRef) == false)
                    {
                        continue;
                    }

                    if (interfaceToStructMap.TryGetValue(@interface, out var structMap) == false)
                    {
                        structMap = new(SymbolEqualityComparer.Default);
                        interfaceToStructMap[@interface] = structMap;
                    }

                    if (structRef == null)
                    {
                        try
                        {
                            structRef = new StructRef(syntax, symbol);
                            structRefArrayBuilder.Add(structRef);
                        }
                        catch (Exception e)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                  s_errorDescriptor_1
                                , syntax.GetLocation()
                                , e.ToUnityPrintableString()
                            ));
                        }
                    }

                    if (structRef.Interfaces.ContainsKey(@interface) == false)
                    {
                        structRef.Interfaces.Add(@interface, interfaceRef);
                    }

                    structMap.Add(symbol, structRef);

                    if (maxStructCount < structMap.Count)
                    {
                        maxStructCount = structMap.Count;
                    }
                }
            }

            structRefs = structRefArrayBuilder.ToImmutable();
        }

        private static void BuildMergedFieldRefList(
              StructMap structMap
            , List<MergedFieldRef> list
            , Queue<MergedFieldRef> pool
        )
        {
            var usedIndexesInList = new HashSet<int>();

            foreach (var kv in structMap)
            {
                usedIndexesInList.Clear();

                var structSymbol = kv.Key;
                var fields = kv.Value.Fields;

                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    var field = fields[fieldIndex];
                    var matchingListIndex = -1;

                    for (int listIndex = 0; listIndex < list.Count; listIndex++)
                    {
                        if (usedIndexesInList.Contains(listIndex) == false)
                        {
                            if (SymbolEqualityComparer.Default.Equals(field.Type, list[listIndex].Type))
                            {
                                matchingListIndex = listIndex;
                                break;
                            }
                        }
                    }

                    if (matchingListIndex < 0)
                    {
                        int newListIndex = list.Count;
                        MergedFieldRef mergedField;

                        if (pool.Count > 0)
                        {
                            mergedField = pool.Dequeue();
                        }
                        else
                        {
                            mergedField = new MergedFieldRef();
                        }

                        mergedField.Type = field.Type;
                        mergedField.Name = $"Field_{field.Type.ToValidIdentifier()}_{newListIndex}";
                        mergedField.StructToFieldMap.Add(structSymbol, field.Name);

                        field.MergedName = mergedField.Name;

                        list.Add(mergedField);
                        usedIndexesInList.Add(newListIndex);
                    }
                    else
                    {
                        var mergedField = list[matchingListIndex];
                        field.MergedName = mergedField.Name;

                        mergedField.StructToFieldMap.Add(structSymbol, field.Name);
                        usedIndexesInList.Add(matchingListIndex);
                    }
                }
            }
        }

        private static void ClearToPool(List<MergedFieldRef> list, Queue<MergedFieldRef> pool)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var item = list[i];
                item.Type = default;
                item.Name = default;
                item.StructToFieldMap.Clear();

                list.RemoveAt(i);
                pool.Enqueue(item);
            }

            list.Clear();
        }

        private static readonly DiagnosticDescriptor s_errorDescriptor_1
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