// The Unity 6000.4 interop output contains incomplete compiler attribute types.
// Local definitions ensure Roslyn does not use their native wrapper constructors.
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
internal sealed class NullableAttribute : Attribute
{
    public NullableAttribute(byte value) { }
    public NullableAttribute(byte[] values) { }
}

[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
internal sealed class NullableContextAttribute : Attribute
{
    public NullableContextAttribute(byte value) { }
}
