// The svg metadata types moved into AJut.UX, beside the shared svg parsing, in the same namespace. These send code
//  built against an AJut.UX.Wpf that still held them on to AJut.UX, so it keeps running without a rebuild.
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(AJut.UX.SvgMetadata))]
[assembly: TypeForwardedTo(typeof(AJut.UX.SvgMetadataStorage))]
[assembly: TypeForwardedTo(typeof(AJut.UX.SvgElementMetadataStorage))]
[assembly: TypeForwardedTo(typeof(AJut.UX.SvgNamespaceMetadataStorage))]
