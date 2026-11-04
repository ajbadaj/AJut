# AJut.UX.AppKit
C# / AppKit (macOS) / dotnet 10 utility library, created by AJ Badarni

## Greetings
I hope you enjoy using ajut.ux. AJut is short for AJ Utilities, (pronounced like the dipping sauce Au Jus). This is the macOS sibling of AJut.UX.WinUI3 and AJut.UX.Wpf: AppKit views over the same models AJut.UX already holds, so a control behaves the same on every stack and only the view is rewritten per toolkit.

## What is in here
- **Theming:** the AJut palette on AppKit. Every control looks sane with no theme loaded (macOS system colors), and adopts the AJut light or dark palette when one is. Colors are dynamic `NSColor`s, so they follow the window's appearance on their own.
- **Layout:** `FillStack`, a stack that can actually fill (an `NSStackView` cannot).
- **Controls:** AppKit views for the AJut controls, each sitting on the toolkit-independent logic in AJut.UX.

## Views are built in code
WPF and WinUI3 controls are XAML first. AppKit's declarative form is Interface Builder (XIB and storyboard files), which needs Xcode to author and does not suit a library of reusable controls, so the usual practice for .NET on macOS is to build views in C#. That is what this library does: each control builds its own view tree in code, and consumers configure it through plain properties.

## Licensing
This software uses an MIT License, see LICENSE - terms and conditions contained therein.

## Final Message
I hope you find use in the greater ajut libraries!

Thanks!
-AJ
