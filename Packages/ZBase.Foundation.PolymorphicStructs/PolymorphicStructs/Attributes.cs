using System;

namespace ZBase.Foundation.PolymorphicStructs
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class SkipGeneratorForAssemblyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class PolymorphicStructInterfaceAttribute : Attribute
    {
        public bool Verbose { get; set; }
    }

    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class PolymorphicStructAttribute : Attribute { }
}
