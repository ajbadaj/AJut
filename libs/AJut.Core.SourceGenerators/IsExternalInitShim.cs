// Polyfill for the C# 9 `init` accessor on netstandard2.0. The compiler emits a reference to
// System.Runtime.CompilerServices.IsExternalInit when an init-only setter is used; net5+ ships
// the type, netstandard2.0 does not, so analyzer/generator projects targeting netstandard2.0
// declare it themselves. Internal so it does not collide with the real one in any consumer.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
