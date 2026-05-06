# The AJut Libraries
C# / dotnet 8 / wpf & winui - utility libraries, created by AJ Badarni
<!-- 👇 nuget package table -->
| | |
|-|-|
|AJut.Core|[![NuGet version (AJut.Core)](https://img.shields.io/nuget/v/AJut.Core.svg?style=flat-square)](https://www.nuget.org/packages/AJut.Core/) [![MIT License](https://img.shields.io/badge/license-MIT-green.svg?style=flat-square)](/LICENSE)|
|AJut.UX|[![NuGet version (AJut.UX)](https://img.shields.io/nuget/v/AJut.UX.svg?style=flat-square)](https://www.nuget.org/packages/AJut.UX.Wpf/) [![MIT License](https://img.shields.io/badge/license-MIT-green.svg?style=flat-square)](/LICENSE)|
|AJut.UX.Wpf|[![NuGet version (AJut.UX.Wpf)](https://img.shields.io/nuget/v/AJut.UX.Wpf.svg?style=flat-square)](https://www.nuget.org/packages/AJut.UX.Wpf/) [![MIT License](https://img.shields.io/badge/license-MIT-green.svg?style=flat-square)](/LICENSE)|
|AJut.UX.WinUI3|[![NuGet version (AJut.UX.Wpf)](https://img.shields.io/nuget/v/AJut.UX.WinUI3.svg?style=flat-square)](https://www.nuget.org/packages/AJut.UX.WinUI3/) [![MIT License](https://img.shields.io/badge/license-MIT-green.svg?style=flat-square)](/LICENSE)|

## Greetings
I hope you enjoy this library. AJut is short for AJ Utilities, (pronounced like the dipping sauce Au Jus).

The primary libraries **`ajut.core`**, **`ajut.ux.wpf`**, and **`ajut.ux.winui3`** (pre-release) are intended to be effectively standalone project enhancers. They are controls, utilities, and paridigms I've built dating back to when I first got into C# and refined over the years.

The goal was to provide a simple project reference that will allow you to build and innovate without having to start completely from scratch or to have to bring in many many libraries to get things done.

Many of these utilities are my takes on ideas that *aren't* new - and yet those ideas have to be rewritten everywhere we develop... and I find that a shame. Other ideas and controls in here are of my own innovation! The unique difference is that everything is in support of how I like to develop, and I hope you will enjoy doing things the AJut way too!

## Licensing
This software uses an MIT License, see [LICENSE](/LICENSE) - terms and conditions contained therin.

## Thanks
As a matter of attribution, several of these utilities I have refined some with a friend, [Ian Good](https://github.com/IGood). Thanks Ian!

## AJson V2

`AJut.Core` ships AJson V2 in `AJut.Text.AJson`. V2 keeps the same shape and entry points as V1 but drops the editing-tracker overhead, replaces per-call heap allocations with pooled buffers, and adds a Roslyn source generator for trimming-friendly compile-time-emitted serialization helpers.

V1 lives in `AJut.Text.AJson.Legacy`, `[Obsolete]` for one release cycle. To migrate: change `using AJut.Text.AJson.Legacy;` -> `using AJut.Text.AJson;`. The fix the typo on the way: `JsonInterpretterSettings` (V1) -> `JsonInterpreterSettings` (V2). API shape is otherwise the same.

### Source generator (`[OptimizeAJson]`)

For a faster, trim-safe path on a type, decorate it with `[OptimizeAJson]`:

```cs
[OptimizeAJson]
public class WireMessage
{
    public string Kind { get; set; }
    public int SequenceNumber { get; set; }
    public byte[] Payload { get; set; }
}
```

The Roslyn generator (bundled with `AJut.Core`'s nupkg, no separate package) emits a compile-time serialization helper per opted-in type and registers it into a dispatch table at module init. `JsonHelper.BuildJsonForObject<T>` and `BuildObjectForJson<T>` route opted-in types through the generated path; unannotated types fall back to reflection.

For a referenced library full of pure-data types you do not control, opt in the whole assembly:

```cs
[assembly: OptimizeAJson(typeof(SomeMarkerTypeFromTheTargetLibrary))]
```

The marker is just a compile-time pointer at the assembly to walk. Generator emits one helper per public type in that assembly.

### Trimming

Source-generated types survive `<TrimMode>full</TrimMode>` cleanly because the emitted code references each property by name (the trimmer keeps them).

Reflection-only types under aggressive trim need either:
- `[OptimizeAJson]` (recommended), or
- `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]` on the holder so the trimmer keeps the relevant members.

The reflection path's public entry points (`JsonHelper.BuildObjectForJson<T>`, `BuildObjectForJson(Type, ...)`) carry the necessary `DynamicallyAccessedMembers` annotations so consumer-side type holders propagate trim safety automatically when annotated.

## Final Message
I hope you are able to find AJut useful!

Thanks!
> AJ
