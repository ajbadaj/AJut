# AJut.UX.WinUI3
C# / WinUI3 / dotnet 8 utility library, created by AJ Badarni

## Greetings
I hope you enjoy using ajut.ux. AJut is short for AJ Utilities, (pronounced like the dipping sauce Au Jus). This ux library contains controls, converters, extensions, content navigation, theming helpers, dark and light themes, utilities - and much more. I've been building these up since I first started developing - through the many side projects I've done over the years. The goal was to provide a simple project reference (ajut.core + ajut.ux.wpf) that will allow you to build and innovate without having to start completely from scratch or to have to bring in many many libraries to get things done.

Many of these are my takes on ideas that aren't new - and yet those ideas have to be rewritten everywhere we develop... and I find that a shame. Many ideas in here are of my own innovation! The unique difference is that everything is in support of how I like to develop, and I hope you will enjoy doing things this way too!

## NuGet Reference

### Adding the Package
Add a `PackageReference` in your `.csproj`:
```xml
<PackageReference Include="AJut.UX.WinUI3" Version="1.0.0.99" />
```

The package ships a pre-built `.pri` file alongside its DLL (Community Toolkit approach). WinAppSDK's
build targets detect and merge it into your app's PRI automatically - no `.targets` injection, no
`ExcludeAssets` workarounds, no manual build steps (like it used to).

This works in all reference scenarios:
- **Direct** `PackageReference` in a WinExe app ✓
- **NuGet → NuGet** chain (your app references a library that references AJut.UX.WinUI3) ✓
- **NuGet → ProjectReference** chain (your app has a ProjectReference to a library that has a PackageReference to AJut.UX.WinUI3) ✓

### App.xaml Setup - Required

AJut controls use `{ThemeResource AJut_*}` keys inside their VSM visual states. Due to a WinUI3 engine
behavior, these keys must be present in `Application.Resources` at runtime - WinUI3's GoToState-triggered
`{ThemeResource}` lookups only search App.Resources, not Generic.xaml. This is the same reason every
WinUI3 app must add `<XamlControlsResources/>`.

You have two options depending on whether you want the full AJut visual theme or just functional defaults.

---

#### Option A - Unthemed (system-neutral default visuals)

Controls render with neutral system-color defaults - they look like standard WinUI3 controls, just
with AJut's layout and behavior. No palette or branded colors applied.

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
            <!-- Required: makes AJut_* brush keys available to control VSM states at runtime -->
            <ResourceDictionary Source="ms-appx:///AJut.UX.WinUI3/Controls/AJutCustomControlStylingDefaults.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

---

#### Option B - Full AJut Theme (dark + light)

Applies the full AJut visual palette to all AJut controls and adapts to the system dark/light theme.
`AllControlThemeOverrides.xaml` (merged flat) provides all `AJut_*` keys via `AJutThemeMapAliasing.xaml`,
so `AJutCustomControlStylingDefaults.xaml` is **not** needed separately.

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
            <!-- AJut control style overrides (flat - provides AJut_* keys for VSM) -->
            <ResourceDictionary Source="ms-appx:///AJut.UX.WinUI3/Resources/AllControlThemeOverrides.xaml"/>
        </ResourceDictionary.MergedDictionaries>
        <!-- Palette colors in ThemeDictionaries so {ThemeResource AJut_Color_*} resolves per-theme -->
        <ResourceDictionary.ThemeDictionaries>
            <ResourceDictionary x:Key="Dark">
                <ResourceDictionary.MergedDictionaries>
                    <ResourceDictionary Source="ms-appx:///AJut.UX.WinUI3/Resources/DarkThemeColorsBase.xaml"/>
                </ResourceDictionary.MergedDictionaries>
            </ResourceDictionary>
            <ResourceDictionary x:Key="Light">
                <ResourceDictionary.MergedDictionaries>
                    <ResourceDictionary Source="ms-appx:///AJut.UX.WinUI3/Resources/LightThemeColorsBase.xaml"/>
                </ResourceDictionary.MergedDictionaries>
            </ResourceDictionary>
        </ResourceDictionary.ThemeDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

> **Note on ordering:** `AllControlThemeOverrides.xaml` references raw `AJut_Brush_*` palette keys
> internally. Those keys are defined in `DarkThemeColorsBase.xaml` / `LightThemeColorsBase.xaml`.
> The palette dictionaries must be in `ThemeDictionaries` (not flat `MergedDictionaries`) so that
> `{ThemeResource AJut_Color_*}` resolves from the correct theme at runtime.

---

### Why Generic.xaml Alone Isn't Enough

If you're curious why adding `XamlControlsResources` and nothing else causes a crash:

WinUI3's `{ThemeResource}` lookup behaves differently depending on *when* it runs:
- **XAML parse time** (control template instantiation): searches Generic.xaml ✓
- **Runtime** via `VisualStateManager.GoToState` (called from `OnApplyTemplate` in C#): only searches App.Resources ✗

AJut control templates call `GoToState` in `OnApplyTemplate` to establish their initial visual state.
If the `AJut_*` keys aren't in App.Resources at that point, you'll see a `MissingKeyException` with
`[Line: 0 Position: 0]` - the `[Line: 0 Position: 0]` is the tell that it's a runtime (C# code path)
failure rather than a XAML parse-time failure.

This is a WinUI3 engine constraint, the same reason `XamlControlsResources` must be in App.Resources
for standard WinUI3 controls to work. Either Option A or Option B above satisfies this requirement.

---

## Licensing
This software uses an MIT License, see LICENSE - terms and conditions contained therein.

## Thanks
As a matter of attribution, several of these utilities I have refined some with a friend, Ian Good. Thanks Ian!

## Final Message
I hope you are able to find ajut.ux.winui useful!

Thanks!
-AJ
