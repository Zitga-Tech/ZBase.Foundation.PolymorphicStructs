using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using ZBase.Foundation.SourceGen;

namespace ZBase.Foundation.PolymorphicStructs.PolymorphicStructSourceGen
{
    partial class PolymorphicStructGenerator
    {
        private static void GenerateEmptyMergedStruct(
              SourceProductionContext context
            , Compilation compilation
            , bool outputSourceGenFiles
            , IEnumerable<InterfaceRef> interfaceRefs
            , StringBuilder sb
        )
        {
            foreach (var interfaceRef in interfaceRefs)
            {
                try
                {
                    var syntaxTree = interfaceRef.Syntax.SyntaxTree;
                    var source = WriteMergedStruct(
                          interfaceRef
                        , Array.Empty<StructRef>()
                        , 0
                        , Array.Empty<MergedFieldRef>()
                        , sb
                    );
                    var sourceFilePath = syntaxTree.GetGeneratedSourceFilePath(compilation.Assembly.Name, GENERATOR_NAME);

                    var outputSource = TypeCreationHelpers.GenerateSourceTextForRootNodes(
                      sourceFilePath
                    , interfaceRef.Syntax
                    , source
                    , context.CancellationToken
                );

                    context.AddSource(
                          syntaxTree.GetGeneratedSourceFileName(GENERATOR_NAME, interfaceRef.Syntax, interfaceRef.StructName)
                        , outputSource
                    );

                    if (outputSourceGenFiles)
                    {
                        SourceGenHelpers.OutputSourceToFile(
                              context
                            , interfaceRef.Syntax.GetLocation()
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
                        , interfaceRef.Syntax.GetLocation()
                        , e.ToUnityPrintableString()
                    ));
                }
            }
        }
    }
}
