using System;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZBase.Foundation.SourceGen;

namespace ZBase.Foundation.PolymorphicStructs.PolymorphicStructSourceGen
{
    public class InterfaceRef
    {
        public InterfaceDeclarationSyntax Syntax { get; }

        public INamedTypeSymbol Symbol { get; }

        public ImmutableArray<ISymbol> Members { get; private set; }

        public string FullContainingNameWithDot { get; private set; }

        public string StructName { get; private set; }

        public InterfaceRef(
              InterfaceDeclarationSyntax syntax
            , INamedTypeSymbol symbol
        )
        {
            this.Syntax = syntax;
            this.Symbol = symbol;

            InitFullContainingName();
            InitStructName();
            InitMembers();
        }

        private void InitFullContainingName()
        {
            var sb = new StringBuilder(Symbol.ToFullName());
            var name = Symbol.Name.AsSpan();
            var startIndex = sb.Length - name.Length;

            sb.Remove(startIndex, name.Length);

            FullContainingNameWithDot = sb.ToString();
        }

        private void InitStructName()
        {
            var nameSpan = Symbol.Name.AsSpan();

            if (nameSpan.Length < 1)
            {
                StructName = "__PolymorphicStruct__";
                return;
            }

            if (nameSpan[0] == 'I'
                && nameSpan.Length > 1
                && char.IsUpper(nameSpan[1])
            )
            {
                StructName = $"{nameSpan.Slice(1).ToString()}";
                return;
            }

            StructName = $"{Symbol.Name}";
        }

        private void InitMembers()
        {
            var memberArrayBuilder = ImmutableArrayBuilder<ISymbol>.Rent();

            GetMembers(Symbol, ref memberArrayBuilder);

            foreach (var symbol in Symbol.AllInterfaces)
            {
                GetMembers(symbol, ref memberArrayBuilder);
            }

            Members = memberArrayBuilder.ToImmutable();
            memberArrayBuilder.Dispose();

            static void GetMembers(
                  INamedTypeSymbol symbol
                , ref ImmutableArrayBuilder<ISymbol> memberArrayBuilder
            )
            {
                foreach (var member in symbol.GetMembers())
                {
                    if (member is IMethodSymbol method
                        && method.MethodKind is (MethodKind.PropertyGet or MethodKind.PropertySet)
                    )
                    {
                        continue;
                    }

                    memberArrayBuilder.Add(member);
                }
            }
        }
    }
}
