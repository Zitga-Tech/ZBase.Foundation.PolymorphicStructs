using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZBase.Foundation.SourceGen;

namespace ZBase.Foundation.PolymorphicStructs.PolymorphicStructSourceGen
{
    public class StructRef
    {
        public StructDeclarationSyntax Syntax { get; }

        public INamedTypeSymbol Symbol { get; }

        public ImmutableArray<FieldRef> Fields { get; }

        public Dictionary<INamedTypeSymbol, InterfaceRef> Interfaces { get; }

        public StructRef(
              StructDeclarationSyntax syntax
            , INamedTypeSymbol symbol
        )
        {
            Syntax = syntax;
            Symbol = symbol;
            Interfaces = new(SymbolEqualityComparer.Default);

            using var fieldArrayBuilder = ImmutableArrayBuilder<FieldRef>.Rent();

            foreach (var member in symbol.GetMembers())
            {
                if (member is IFieldSymbol field && field.IsStatic == false)
                {
                    if (field.AssociatedSymbol is IPropertySymbol property)
                    {
                        fieldArrayBuilder.Add(new FieldRef(property.Type, property.Name));
                    }
                    else
                    {
                        fieldArrayBuilder.Add(new FieldRef(field.Type, field.Name));
                    }

                    continue;
                }
            }

            Fields = fieldArrayBuilder.ToImmutable();
        }
    }
}
