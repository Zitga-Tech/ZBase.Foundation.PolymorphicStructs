using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using ZBase.Foundation.SourceGen;

namespace ZBase.Foundation.PolymorphicStructs.PolymorphicStructSourceGen
{
    partial class PolymorphicStructGenerator
    {
        private static void GenerateMergedStruct(
              SourceProductionContext context
            , Compilation compilation
            , bool outputSourceGenFiles
            , InterfaceRef interfaceRef
            , IEnumerable<StructRef> structRefs
            , ulong structRefCount
            , IReadOnlyCollection<MergedFieldRef> mergedFieldRefList
            , StringBuilder sb
            , CancellationToken token
        )
        {
            try
            {
                var syntaxTree = interfaceRef.Syntax.SyntaxTree;
                var source = WriteMergedStruct(
                      interfaceRef
                    , structRefs
                    , structRefCount
                    , mergedFieldRefList
                    , sb
                    , token
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

        private static string WriteMergedStruct(
              InterfaceRef interfaceRef
            , IEnumerable<StructRef> structRefs
            , ulong structRefCount
            , IReadOnlyCollection<MergedFieldRef> mergedFieldRefList
            , StringBuilder sb
            , CancellationToken token
        )
        {
            var scopePrinter = new SyntaxNodeScopePrinter(Printer.DefaultLarge, interfaceRef.Syntax.Parent);
            var p = scopePrinter.printer;

            p.PrintLine("#pragma warning disable");
            p.PrintEndLine();

            p = p.IncreasedIndent();

            p.PrintLine("[global::System.Serializable]");
            p.PrintLine(GENERATED_CODE);
            p.PrintLine(EXCLUDE_COVERAGE);
            p.PrintBeginLine();
            p.Print($"public partial struct {interfaceRef.StructName}")
                .Print($" : {interfaceRef.Symbol.ToFullName()}")
                .PrintEndLine();
            p.OpenScope();
            {
                WriteFields(ref p, mergedFieldRefList);
                WriteIsType(ref p, interfaceRef, structRefs, structRefCount);
                WriteMembers(ref p, interfaceRef, structRefs, structRefCount, sb, token);
                WriteGetTypeIdMethods(ref p, interfaceRef, structRefs);
                WriteEnum(ref p, structRefs, structRefCount);
                WriteGenericTypeIdStruct(ref p, interfaceRef, structRefs, structRefCount);
            }
            p.CloseScope();

            p = p.DecreasedIndent();
            return p.Result;
        }

        private static void WriteFields(ref Printer p, IReadOnlyCollection<MergedFieldRef> mergedFieldRefList)
        {
            p.PrintLine("public TypeId CurrentTypeId;");
            p.PrintEndLine();

            if (mergedFieldRefList.Count < 1)
            {
                return;
            }

            foreach (var field in mergedFieldRefList)
            {
                p.PrintBeginLine("public ")
                    .Print(field.Type.ToFullName())
                    .Print(" ")
                    .Print(field.Name)
                    .PrintEndLine(";");
            }

            p.PrintEndLine();
        }

        private static void WriteIsType(
              ref Printer p
            , InterfaceRef interfaceRef
            , IEnumerable<StructRef> structRefs
            , ulong structRefCount
        )
        {
            p.PrintLine(AGGRESSIVE_INLINING).PrintLine(GENERATED_CODE).PrintLine(EXCLUDE_COVERAGE);
            p.PrintLine($"public readonly bool IsType<T>() where T : struct, {interfaceRef.Symbol.ToFullName()}");
            p.OpenScope();
            {
                p.PrintLine($"return GetTypeId<T>() == this.CurrentTypeId;");
            }
            p.CloseScope();
            p.PrintEndLine();

            foreach (var structRef in structRefs)
            {
                p.PrintLine(AGGRESSIVE_INLINING).PrintLine(GENERATED_CODE).PrintLine(EXCLUDE_COVERAGE);
                p.PrintLine($"public readonly bool IsType({structRef.Symbol.ToFullName()} _)");
                p.OpenScope();
                {
                    p.PrintLine($"return GetTypeId<{structRef.Symbol.ToFullName()}>() == this.CurrentTypeId;");
                }
                p.CloseScope();
                p.PrintEndLine();
            }
        }

        private static void WriteMembers(
              ref Printer p
            , InterfaceRef interfaceRef
            , IEnumerable<StructRef> structRefs
            , ulong structRefCount
            , StringBuilder sb
            , CancellationToken token
        )
        {
            foreach (var member in interfaceRef.Members)
            {
                if (member is IMethodSymbol method)
                {
                    WriteMethod(
                          ref p
                        , structRefs
                        , structRefCount
                        , method
                        , sb
                        , token
                    );
                    continue;
                }

                if (member is IPropertySymbol property)
                {
                    WriteProperty(
                          ref p
                        , interfaceRef
                        , structRefs
                        , structRefCount
                        , property
                        , token
                    );
                    continue;
                }
            }
        }

        private static void WriteMethod(
              ref Printer p
            , IEnumerable<StructRef> structRefs
            , ulong structRefCount
            , IMethodSymbol method
            , StringBuilder sb
            , CancellationToken token
        )
        {
            var returnType = method.ReturnsVoid ? "void" : method.ReturnType.ToFullName();

            WriteAttributes(ref p, method, token);
            p.PrintLine(GENERATED_CODE).PrintLine(EXCLUDE_COVERAGE);
            p.PrintBeginLine($"public {GetReturnRefKind(method.RefKind)}{returnType} {method.Name}(");
            {
                WriteParameters(ref p, method);
            }
            p.PrintEndLine(")");

            var callClause = BuildCallClause(sb, method);
            var assignOutParams = BuildAssignOutParams(sb, method);

            WriteMethodBody(ref p, structRefs, structRefCount, method, false, callClause, assignOutParams);

            p.PrintEndLine();

            WriteAttributes(ref p, method, token);
            p.PrintLine(GENERATED_CODE).PrintLine(EXCLUDE_COVERAGE);
            p.PrintBeginLine($"partial void {GetDefaultMethodName(method)}(");
            {
                WriteParameters(ref p, method, outToRef: true);

                if (method.ReturnsVoid == false)
                {
                    var resultVarName = GetDefaultResultVarName(method);
                    p.Print($", ref {method.ReturnType.ToFullName()} {resultVarName}");
                }
            }
            p.PrintEndLine(");");

            p.PrintEndLine();

            static string BuildCallClause(
                  StringBuilder sb
                , IMethodSymbol method
            )
            {
                var parameters = method.Parameters;
                var lastParamIndex = parameters.Length - 1;

                sb.Clear();
                sb.Append($"instance.{method.Name}(");

                for (var i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    sb.Append($"{GetRefKind(param.RefKind)}{param.Name}");

                    if (i < lastParamIndex)
                    {
                        sb.Append(", ");
                    }
                }

                sb.Append(");");
                return sb.ToString();
            }

            static string BuildAssignOutParams(
                  StringBuilder sb
                , IMethodSymbol method
            )
            {
                sb.Clear();

                foreach (var param in method.Parameters)
                {
                    if (param.RefKind == RefKind.Out)
                    {
                        sb.Append($"{param.Name} = default;").Append('\n');
                    }
                }

                return sb.ToString();
            }
        }

        private static void WriteParameters(
              ref Printer p
            , IMethodSymbol method
            , bool outToRef = false
        )
        {
            var parameters = method.Parameters;
            var lastParamIndex = parameters.Length - 1;

            for (var i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];

                p.Print(GetRefKind(param.RefKind, outToRef))
                    .Print(param.Type.ToFullName())
                    .Print(" ")
                    .Print(param.Name);

                if (i < lastParamIndex)
                {
                    p.Print(", ");
                }
            }
        }

        private static void WriteProperty(
              ref Printer p
            , InterfaceRef interfaceRef
            , IEnumerable<StructRef> structRefs
            , ulong structRefCount
            , IPropertySymbol property
            , CancellationToken token
        )
        {
            p.PrintLine(GENERATED_CODE).PrintLine(EXCLUDE_COVERAGE);
            p.PrintLine($"public {GetReturnRefKind(property.RefKind)}{property.Type.ToFullName()} {property.Name}");
            p.OpenScope();
            {
                if (property.GetMethod != null)
                {
                    WriteAttributes(ref p, property.GetMethod, token);

                    p.PrintLine("get");
                    WriteMethodBody(
                          ref p
                        , structRefs
                        , structRefCount
                        , property.GetMethod
                        , true
                        , $"instance.{property.Name};"
                        , ""
                    );
                }

                if (property.SetMethod != null)
                {
                    WriteAttributes(ref p, property.SetMethod, token);

                    p.PrintLine("set");
                    WriteMethodBody(
                          ref p
                        , structRefs
                        , structRefCount
                        , property.SetMethod
                        , true
                        , $"instance.{property.Name} = value;"
                        , ""
                    );
                }
            }
            p.CloseScope();
        }

        private static void WriteAttributes(
              ref Printer p
            , ISymbol symbol
            , CancellationToken token
        )
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.ApplicationSyntaxReference is not SyntaxReference syntaxRef)
                {
                    continue;
                }

                var syntax = syntaxRef.GetSyntax(token);
                p.PrintBeginLine("[").Print(syntax.ToFullString()).PrintEndLine("]");
            }
        }

        private static void WriteMethodBody(
              ref Printer p
            , IEnumerable<StructRef> structRefs
            , ulong structRefCount
            , IMethodSymbol method
            , bool isPropertyBody
            , string callClause
            , string assignOutParams
        )
        {
            if (structRefCount < 1)
            {
                p.OpenScope();
                {
                    if (method.RefKind is (RefKind.Ref or RefKind.RefReadOnly))
                    {
                        p.PrintBeginLine("throw new global::System.InvalidOperationException(\"");
                        p.Print("Cannot return any reference from the default case.");
                        p.PrintEndLine("\");");
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(assignOutParams) == false)
                        {
                            p.PrintLine(assignOutParams);
                        }

                        if (isPropertyBody)
                        {
                            p.PrintLine("return default;");
                        }
                        else
                        {
                            WriteDefaultMethodBranch(ref p, method);
                        }
                    }
                }
                p.CloseScope();

                return;
            }

            p.OpenScope();
            {
                p.PrintLine("switch (this.CurrentTypeId)");
                p.OpenScope();
                {
                    foreach (var structRef in structRefs)
                    {
                        p.PrintLine($"case TypeId.{structRef.Symbol.ToValidIdentifier()}:");
                        p.OpenScope();
                        {
                            p.PrintLine($"{structRef.Symbol.ToFullName()} instance = this;");
                            p.PrintBeginLine();

                            if (method.ReturnsVoid == false)
                            {
                                if (method.RefKind == RefKind.Ref)
                                {
                                    p.Print("ref var result = ref ");
                                }
                                else if (method.RefKind == RefKind.RefReadOnly)
                                {
                                    p.Print("ref readonly var result = ref ");
                                }
                                else
                                {
                                    p.Print("var result = ");
                                }
                            }

                            p.Print(callClause).PrintEndLine();

                            if (structRef.Fields.Length > 0)
                            {
                                p.PrintLine("this = instance;");
                            }

                            if (method.ReturnsVoid)
                            {
                                p.PrintLine("return;");
                            }
                            else
                            {
                                p.PrintBeginLine("return ");

                                if (method.RefKind is (RefKind.Ref or RefKind.RefReadOnly))
                                {
                                    p.Print("ref ");
                                }

                                p.PrintEndLine("result;");
                            }
                        }
                        p.CloseScope();
                        p.PrintEndLine();
                    }

                    p.PrintLine("default:");
                    p.OpenScope();
                    {
                        if (method.RefKind is (RefKind.Ref or RefKind.RefReadOnly))
                        {
                            p.PrintBeginLine("throw new global::System.InvalidOperationException(\"");
                            p.Print("Cannot return any reference from the default case.");
                            p.PrintEndLine("\");");
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(assignOutParams) == false)
                            {
                                p.PrintLine(assignOutParams);
                            }

                            if (isPropertyBody)
                            {
                                if (method.ReturnsVoid)
                                {
                                    p.PrintLine("return;");
                                }
                                else
                                {
                                    p.PrintLine("return default;");
                                }
                            }
                            else
                            {
                                WriteDefaultMethodBranch(ref p, method);
                            }
                        }
                    }
                    p.CloseScope();
                }
                p.CloseScope();
            }
            p.CloseScope();

            static void WriteDefaultMethodBranch(
                  ref Printer p
                , IMethodSymbol method
            )
            {
                if (method.ReturnsVoid)
                {
                    WriteDefaultMethodCall(ref p, method);
                    p.PrintLine("return;");
                }
                else
                {
                    var resultVarName = GetDefaultResultVarName(method);
                    p.PrintLine($"var {resultVarName} = default({method.ReturnType.ToFullName()});");
                    p.PrintEndLine();
                    WriteDefaultMethodCall(ref p, method, resultVarName);
                    p.PrintEndLine();
                    p.PrintLine($"return {resultVarName};");
                }
            }

            static void WriteDefaultMethodCall(
                  ref Printer p
                , IMethodSymbol method
                , string resultVarName = ""
            )
            {
                var parameters = method.Parameters;
                var lastParamIndex = parameters.Length - 1;

                p.PrintBeginLine($"{GetDefaultMethodName(method)}(");

                for (var i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];

                    p.Print(GetRefKind(param.RefKind, true))
                        .Print(param.Name);

                    if (i < lastParamIndex)
                    {
                        p.Print(", ");
                    }
                }

                if (string.IsNullOrWhiteSpace(resultVarName) == false)
                {
                    p.Print($", ref {resultVarName}");
                }

                p.PrintEndLine(");");
            }
        }

        private static void WriteGetTypeIdMethods(
              ref Printer p
            , InterfaceRef interfaceRef
            , IEnumerable<StructRef> structRefs
        )
        {
            p.PrintLine(AGGRESSIVE_INLINING).PrintLine(GENERATED_CODE).PrintLine(EXCLUDE_COVERAGE);
            p.PrintLine($"public static TypeId GetTypeId<T>() where T : struct, {interfaceRef.Symbol.ToFullName()}");
            p.OpenScope();
            {
                p.PrintLine("return TypeId<T>.Value;");
            }
            p.CloseScope();
            p.PrintEndLine();

            foreach (var structRef in structRefs)
            {
                p.PrintLine(AGGRESSIVE_INLINING).PrintLine(GENERATED_CODE).PrintLine(EXCLUDE_COVERAGE);
                p.PrintLine($"public static TypeId GetTypeId(in {structRef.Symbol.ToFullName()} _)");
                p.OpenScope();
                {
                    p.PrintLine($"return TypeId.{structRef.Symbol.ToValidIdentifier()};");
                }
                p.CloseScope();
                p.PrintEndLine();
            }
        }

        private static void WriteEnum(
              ref Printer p
            , IEnumerable<StructRef> structRefs
            , ulong structRefCount
        )
        {
            string underlyingType;

            if (structRefCount <= 256)
            {
                underlyingType = "byte";
            }
            else if (structRefCount <= 65536)
            {
                underlyingType = "ushort";
            }
            else if (structRefCount <= 4294967296)
            {
                underlyingType = "uint";
            }
            else
            {
                underlyingType = "ulong";
            }

            p.PrintLine(GENERATED_CODE);
            p.PrintLine($"public enum TypeId : {underlyingType}");
            p.OpenScope();
            {
                p.PrintLine("Undefined = 0,");

                foreach (var structRef in structRefs)
                {
                    p.PrintBeginLine()
                        .Print(structRef.Symbol.ToValidIdentifier())
                        .PrintEndLine(",");
                }
            }
            p.CloseScope();
            p.PrintEndLine();
        }

        private static void WriteGenericTypeIdStruct(
              ref Printer p
            , InterfaceRef interfaceRef
            , IEnumerable<StructRef> structRefs
            , ulong structRefCount
        )
        {
            if (structRefCount > 0)
            {
                p.PrintLine(GENERATED_CODE).PrintLine(EXCLUDE_COVERAGE);
                p.PrintLine($"static {interfaceRef.StructName}()");
                p.OpenScope();
                {
                    foreach (var structRef in structRefs)
                    {
                        p.PrintLine($"TypeId<{structRef.Symbol.ToFullName()}>.Value = TypeId.{structRef.Symbol.ToValidIdentifier()};");
                    }
                }
                p.CloseScope();
                p.PrintEndLine();
            }

            p.PrintLine(GENERATED_CODE).PrintLine(EXCLUDE_COVERAGE);
            p.PrintLine($"private struct TypeId<T> where T : struct, {interfaceRef.Symbol.ToFullName()}");
            p.OpenScope();
            {
                p.PrintLine($"public static {interfaceRef.StructName}.TypeId Value;");
            }
            p.CloseScope();
            p.PrintEndLine();
        }

        private static string GetDefaultMethodName(IMethodSymbol method)
            => $"{method.Name}_Default";

        private static string GetDefaultResultVarName(IMethodSymbol method)
            => $"{GetDefaultMethodName(method)}_result";

        private static string GetReturnRefKind(RefKind refKind)
        {
            return refKind switch {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.RefReadOnly => "ref readonly ",
                _ => "",
            };
        }

        private static string GetRefKind(RefKind refKind)
        {
            return refKind switch {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => "",
            };
        }

        private static string GetRefKind(RefKind refKind, bool outToRef)
        {
            return refKind switch {
                RefKind.Ref => "ref ",
                RefKind.Out => outToRef ? "ref " : "out ",
                RefKind.In => "in ",
                _ => "",
            };
        }
    }
}
