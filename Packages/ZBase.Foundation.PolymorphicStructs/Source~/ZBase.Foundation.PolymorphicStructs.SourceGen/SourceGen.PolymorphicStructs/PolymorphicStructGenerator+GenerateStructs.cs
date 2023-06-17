using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using ZBase.Foundation.SourceGen;

namespace ZBase.Foundation.PolymorphicStructs.PolymorphicStructSourceGen
{
    partial class PolymorphicStructGenerator
    {
        private static void GenerateStructs(
              SourceProductionContext context
            , Compilation compilation
            , bool outputSourceGenFiles
            , ImmutableArray<StructRef> structRefs
        )
        {
            foreach (var structRef in structRefs)
            {
                try
                {
                    var syntaxTree = structRef.Syntax.SyntaxTree;
                    var source = WriteStruct(structRef);
                    var sourceFilePath = syntaxTree.GetGeneratedSourceFilePath(compilation.Assembly.Name, GENERATOR_NAME);

                    var outputSource = TypeCreationHelpers.GenerateSourceTextForRootNodes(
                          sourceFilePath
                        , structRef.Syntax
                        , source
                        , context.CancellationToken
                    );

                    context.AddSource(
                          syntaxTree.GetGeneratedSourceFileName(GENERATOR_NAME, structRef.Syntax, structRef.Symbol.ToValidIdentifier())
                        , outputSource
                    );

                    if (outputSourceGenFiles)
                    {
                        SourceGenHelpers.OutputSourceToFile(
                              context
                            , structRef.Syntax.GetLocation()
                            , sourceFilePath
                            , outputSource
                        );
                    }
                }
                catch (Exception e)
                {
                    if (e is OperationCanceledException)
                    {
                        throw;
                    }

                    context.ReportDiagnostic(Diagnostic.Create(
                          s_errorDescriptor_1
                        , structRef.Syntax.GetLocation()
                        , e.ToUnityPrintableString()
                    ));
                }
            }
        }

        private static string WriteStruct(StructRef structRef)
        {
            var scopePrinter = new SyntaxNodeScopePrinter(Printer.DefaultLarge, structRef.Syntax.Parent);
            var p = scopePrinter.printer;

            p.PrintLine("#pragma warning disable");
            p.PrintEndLine();

            p = p.IncreasedIndent();

            p.PrintLine($"partial struct {structRef.Syntax.Identifier.Text}");
            p.OpenScope();
            {
                WriteConstructor(ref p, structRef);

                foreach (var interfaceRef in structRef.Interfaces.Values)
                {
                    WriteImplicitToStruct(ref p, interfaceRef, structRef);
                    WriteImplicitFromStruct(ref p, interfaceRef, structRef);
                }
            }
            p.CloseScope();

            p = p.DecreasedIndent();
            return p.Result;
        }

        private static void WriteConstructor(
              ref Printer p
            , StructRef structRef
        )
        {
            p.PrintLine(GENERATED_CODE).PrintLine(EXCLUDE_COVERAGE);
            p.PrintLine($"public {structRef.Syntax.Identifier.Text}(");
            p = p.IncreasedIndent();
            {
                var fields = structRef.Fields;

                for (var i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    var comma = i > 0 ? ", " : "  ";

                    p.PrintLine($"{comma}{field.Type.ToFullName()} arg_{field.Name}");
                }
            }
            p = p.DecreasedIndent();
            p.PrintLine(")");
            p.OpenScope();
            {
                foreach (var field in structRef.Fields)
                {
                    p.PrintLine($"this.{field.Name} = arg_{field.Name};");
                }
            }
            p.CloseScope();
            p.PrintEndLine();
        }

        private static void WriteImplicitToStruct(
              ref Printer p
            , InterfaceRef interfaceRef
            , StructRef structRef
        )
        {
            p.PrintLine(AGGRESSIVE_INLINING).PrintLine(GENERATED_CODE).PrintLine(EXCLUDE_COVERAGE);
            p.PrintLine($"public static implicit operator {structRef.Syntax.Identifier.Text}(in {interfaceRef.FullContainingNameWithDot}{interfaceRef.StructName} value)");
            p.OpenScope();
            {
                p.PrintLine($"return new {structRef.Syntax.Identifier.Text}(");
                p = p.IncreasedIndent();
                {
                    var fields = structRef.Fields;

                    for (var i = 0; i < fields.Length; i++)
                    {
                        var field = fields[i];
                        var comma = i > 0 ? ", " : "  ";

                        p.PrintLine($"{comma}value.{field.MergedName}");
                    }
                }
                p = p.DecreasedIndent();
                p.PrintLine(");");
            }
            p.CloseScope();
            p.PrintEndLine();
        }

        private static void WriteImplicitFromStruct(
              ref Printer p
            , InterfaceRef interfaceRef
            , StructRef structRef
        )
        {
            var mergedStructName = $"{interfaceRef.FullContainingNameWithDot}{interfaceRef.StructName}";
            var @in = structRef.Symbol.IsReadOnly ? "in " : "";

            p.PrintLine(AGGRESSIVE_INLINING).PrintLine(GENERATED_CODE).PrintLine(EXCLUDE_COVERAGE);
            p.PrintLine($"public static implicit operator {mergedStructName}({@in}{structRef.Syntax.Identifier.Text} value)");
            p.OpenScope();
            {
                p.PrintLine($"return new {mergedStructName} {{");
                p = p.IncreasedIndent();
                {
                    p.PrintLine($"CurrentTypeId = {mergedStructName}.TypeId.{structRef.Symbol.ToValidIdentifier()},");

                    var fields = structRef.Fields;

                    foreach (var field in fields)
                    {
                        p.PrintLine($"{field.MergedName} = value.{field.Name},");
                    }
                }
                p = p.DecreasedIndent();
                p.PrintLine("};");
            }
            p.CloseScope();
            p.PrintEndLine();
        }
    }
}
