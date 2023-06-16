using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZBase.Foundation.PolymorphicStructs.PolymorphicStructSourceGen
{
    public class StructRef
    {
        public StructDeclarationSyntax Syntax { get; set; }

        public ITypeSymbol Symbol { get; set; }
    }
}
