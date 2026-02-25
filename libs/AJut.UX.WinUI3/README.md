# AJut.UX.WinUI3
C# / WinUI3 / dotnet 8 utility library, created by AJ Badarni

## Greetings
I hope you enjoy using ajut.ux. AJut is short for AJ Utilities, (pronounced like the dipping sauce Au Jus). This ux library contains controls, converters, extensions, content navigation, theming helpers, dark and light themes, utilities - and much more. I've been building these up since I first started developing - through the many side projects I've done over the years. The goal was to provide a simple project reference (ajut.core + ajut.ux.wpf) that will allow you to build and innovate without having to start completely from scratch or to have to bring in many many libraries to get things done.

Many of these are my takes on ideas that aren't new - and yet those ideas have to be rewritten everywhere we develop... and I find that a shame. Many ideas in here are of my own innovation! The unique difference is that everything is in support of how I like to develop, and I hope you will enjoy doing things this way too!

## Getting Started — NuGet Reference Requirements

### Direct NuGet reference (simplest)
Add a direct `PackageReference` in your WinExe `.csproj`:
```xml
<PackageReference Include="AJut.UX.WinUI3" Version="1.0.0.90" />
```
The package's `build/AJut.UX.WinUI3.targets` file automatically injects the library's XAML
files into your app's build as compiled `Page` items. This produces the correct
`ms-appx:///AJut.UX.WinUI3/...` PRI URIs that the control templates reference at runtime.

### When your app only references a library that uses AJut.UX.WinUI3
NuGet's `buildTransitive/` folder is used by this package (≥ v1.0.0.88) to propagate
the `.targets` injection through NuGet-to-NuGet chains. However, **NuGet→ProjectReference
chains do not propagate build assets** — the targets only run in the immediate consumer's
build context, not in a downstream app project.

**Rule of thumb:** Any WinExe project that uses `AJut.UX.WinUI3` controls — whether directly
or through a chain of library references — must have a **direct** `PackageReference` to
`AJut.UX.WinUI3` in its `.csproj`. This is necessary for PRI injection to happen in the
final app's build.

### Avoiding double-injection (library + app both reference the package)
If your intermediate library also has a `PackageReference` to `AJut.UX.WinUI3` (e.g. because
it uses controls from the library) AND the consuming app has its own direct reference, the
`.targets` injection would run twice, causing duplicate `Page` build errors.

Fix this by suppressing build assets in the **library's** reference:
```xml
<!-- In the library .csproj — suppresses .targets injection, keeps the DLL reference -->
<PackageReference Include="AJut.UX.WinUI3" Version="1.0.0.90">
  <ExcludeAssets>build;buildTransitive;buildMultitargeting;contentFiles</ExcludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```
The app's direct reference (without those exclusions) then handles the injection once.

### Theming
Controls have safe built-in defaults and work without any AJut theme loaded. To apply the
full AJut visual theme, merge these into your `Application.Resources`:
```xml
<ResourceDictionary Source="ms-appx:///AJut.UX.WinUI3/Resources/DarkThemeColorsBase.xaml"/>
<ResourceDictionary Source="ms-appx:///AJut.UX.WinUI3/Resources/AllControlThemeOverrides.xaml"/>
```

## The Journey Here
It was quite a journey through the WinUI3 NuGet packaging rabbit hole, but the key insight that made it work was the Page vs Content distinction — WinUI3 needs XAML compiled at build time, not deployed as loose files.

Quick summary of what the build/AJut.UX.WinUI3.targets hook does for any consumer:
 - Injects the library's XAML files into the consuming app's build as Page items
 - The app's XAML compiler processes them: String → Uri conversion, XBF generation, PRI entries
 - This is key for getting working xaml like you would from a project reference, but via NuGet

## Licensing
This software uses an MIT License, see LICENSE - terms and conditions contained therin.

## Thanks
As a matter of attribution, several of these utilities I have refined some with a friend, Ian Good. Thanks Ian!

## Final Message
I hope you are able to find ajut.ux.winui useful!

Thanks!
-AJ