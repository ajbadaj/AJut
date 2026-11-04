# AJut.UX.UIKit
C# / UIKit (iOS) / dotnet 10 utility library, created by AJ Badarni

## Greetings
I hope you enjoy using ajut.ux. AJut is short for AJ Utilities, (pronounced like the dipping sauce Au Jus). This is the iOS sibling of AJut.UX.WinUI3 and AJut.UX.Wpf: UIKit views built over the same shared models in AJut.UX, so a control behaves the same way on every stack and only the view is native.

## How it differs from the XAML stacks
UIKit has no XAML, so there are no templates, resource dictionaries, or converters here. Each control is a UIView (or UIControl) subclass you create and configure in code, the way UIKit itself works. Colors are UIColor, and the defaults are UIKit's own system colors, so every control looks right in light and dark mode with no theme loaded at all.

## NuGet Reference

### Adding the Package
Add a `PackageReference` in your `.csproj` (the app targets `net10.0-ios`, iOS 15 or later):
```xml
<PackageReference Include="AJut.UX.UIKit" Version="..." />
```

### Using the controls
Make a control, set it up, and add it to a view, the same as any UIKit view:
```cs
using AJut.UX.Controls;
using AJut.UX.Helpers;

var accent = new ColorEditIngressControl { EditColor = new ColorArgb(0xFF, 0x2E, 0xCC, 0x71) };
var count = new NumericEditor { Value = 3, Minimum = 0, Maximum = 10 };
stack.AddArrangedSubview(accent);
stack.AddArrangedSubview(count);
```

### Binding to your models
UIKit has no binding to a C# property, so a `BindingSet` (from AJut.UX, in the `AJut.UX.CodeBinding` namespace) does it in code, with this package adding the UIKit views to it in the same namespace. Make one per screen on the main thread, route every hookup through it, and dispose it when the screen goes away, which undoes everything it hooked up:
```cs
using AJut.UX.CodeBinding;

m_bindings = new BindingSet();
m_bindings.BindText(nameField, settings, nameof(Settings.Name), () => settings.Name, v => settings.Name = v);
m_bindings.BindOn(autoSave, settings, nameof(Settings.AutoSave), () => settings.AutoSave, v => settings.AutoSave = v);
m_bindings.BindEditColor(accentSwatch, settings, nameof(Settings.Accent), () => settings.Accent, v => settings.Accent = v);
m_bindings.BindCommand(saveButton, settings.SaveCommand);

// When the screen is torn down
m_bindings.Dispose();
```
A model can raise PropertyChanged from any thread; the view is always updated on the main thread. Any UIControl that sends a control event when the user edits it binds the same way, through `BindControl`.

### Light and dark, and the AJut theme
With no theme set up, every control uses UIKit's system colors, so it looks right in light and in dark. To give the controls the AJut palette instead, set `ThemedColors.IsThemeLoaded = true` at startup, before the first control is made.

To let the user pick light, dark, or the system's own setting, make one `AppThemeManager` in `FinishedLaunching` and keep it for the life of the app. It sets the light or dark of every window, including windows shown later, and keeps the choice in `NSUserDefaults`:

```csharp
this.Theme = new AppThemeManager();
this.Theme.ThemeConfiguration = eAppThemeConfiguration.Dark;
```

With the AJut theme loaded it also gives each window the AJut accent as its tint, which UIKit's own buttons, sliders and text carets take, and every `UISwitch` the AJut on color (`StockControlTint`).

## Licensing
This software uses an MIT License, see LICENSE - terms and conditions contained therin.

## Thanks
As a matter of attribution, several of these utilities I have refined some with a friend, Ian Good. Thanks Ian!

Thanks!
-AJ
