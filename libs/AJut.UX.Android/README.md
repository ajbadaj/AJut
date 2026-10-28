# AJut.UX.Android
C# / Android Views / dotnet 10 utility library, created by AJ Badarni

## Greetings
I hope you enjoy using ajut.ux. AJut is short for AJ Utilities, (pronounced like the dipping sauce Au Jus). AJut.UX.Android is the Android Views sibling of AJut.UX.WinUI3 and AJut.UX.Wpf: the same controls, built over the same shared models in AJut.UX, so the behavior you know from the desktop stacks carries over to a phone. It starts with the controls that make sense on a phone.

## NuGet Reference

### Adding the Package
Add a `PackageReference` in your `.csproj` (the app targets `net10.0-android`, API 26 or later):
```xml
<PackageReference Include="AJut.UX.Android" Version="..." />
```

### Using the controls in a layout
Every control has a registered name, and takes its settings as layout attributes in the `app` namespace:
```xml
<LinearLayout xmlns:android="http://schemas.android.com/apk/res/android"
    xmlns:app="http://schemas.android.com/apk/res-auto"
    android:layout_width="match_parent"
    android:layout_height="wrap_content">

    <ajut.ux.controls.AutoBrushButton
        android:layout_width="wrap_content"
        android:layout_height="wrap_content"
        android:text="Save"
        app:ajutPropagatedAccentColor="#0D83D8" />

    <ajut.ux.controls.ColorEditIngressControl
        android:layout_width="120dp"
        android:layout_height="32dp"
        app:ajutEditColor="#FF2ECC71" />
</LinearLayout>
```

### Binding to your models
Android layouts have no binding to a C# property, so a `BindingSet` (from AJut.UX, in the `AJut.UX.CodeBinding` namespace) does it in code, with this package adding the Android views to it in the same namespace. Make one per page on the main thread, route every hookup through it, and dispose it when the page goes away, which undoes everything it hooked up:
```cs
using AJut.UX.CodeBinding;

m_bindings = new BindingSet();
m_bindings.BindText(nameField, settings, nameof(Settings.Name), () => settings.Name, v => settings.Name = v);
m_bindings.BindChecked(autoSave, settings, nameof(Settings.AutoSave), () => settings.AutoSave, v => settings.AutoSave = v);
m_bindings.BindEditColor(accentSwatch, settings, nameof(Settings.Accent), () => settings.Accent, v => settings.Accent = v);
m_bindings.BindCommand(saveButton, settings.SaveCommand);

// When the page is torn down
m_bindings.Dispose();
```
A model can raise PropertyChanged from any thread; the view is always updated on the main thread.

### Light and dark
With no theme set up, every control uses plain defaults that follow the app's night mode, so it looks right in light and in dark.

## Licensing
This software uses an MIT License, see LICENSE - terms and conditions contained therein.

## Thanks
As a matter of attribution, several of these utilities I have refined some with a friend, Ian Good. Thanks Ian!

## Final Message
I hope you are able to find ajut.ux.android useful!

Thanks!
-AJ
