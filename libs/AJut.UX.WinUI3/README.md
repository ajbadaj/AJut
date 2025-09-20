# <⚠️ YE BE WARNED ⚠️>

Usage of AJut is at your own risk, and in addition this is pre-release version of the AJut.UX.WinUI3 library is especially so. I will remove this whole warning section once this exits pre-release.

## Shortcomings...
The following are shortcomings of using this library in this state (non-exhaustive, just the highlights). In addition, there is a great Windows Community Toolkit which aims to be a similar (and at this stage for AJut.UX.WinUI3 better) version of these kinds of support utilities - however I'm trying to get things done in the AJut way.

### Theming
There are still some mixed issues with theming. This is a difficult thing to do "right" in the first place.
 - It could be that coming from WPF I'm doing this wrong, but in my oppinion windows jumped from one set of theming mistakes with wpf, and built new theming mistakes with WinUI
 - My approach is to continue using the color & brush names used in the WPF version, which I think are named better. I may also consider restyling everything in WinUI3 again but... I don't wanna
### Controls
There are a few missing controls
 - **NumericEditor**: which could be ported over (I don't like the WinUI3 number box or whatever)
 - **Property Grid:** this is a more gaping loss (especially highlighted by AJut.UX which does hold the common property grid elements that will be shared)
 - **FlatTrreeListControl:** something I'd like and need for the above
### Docking Framework
This one is probably going to be much more time consuming. It could even be that this library exits pre-release and then docking framework is only added later.

# </⚠️>

# AJut.UX.WinUI3
C# / WinUI3 / dotnet 8 utility library, created by AJ Badarni

## Greetings
I hope you enjoy using ajut.ux. AJut is short for AJ Utilities, (pronounced like the dipping sauce Au Jus). This ux library contains controls, converters, extensions, content navigation, theming helpers, dark and light themes, utilities - and much more. I've been building these up since I first started developing - through the many side projects I've done over the years. The goal was to provide a simple project reference (ajut.core + ajut.ux.wpf) that will allow you to build and innovate without having to start completely from scratch or to have to bring in many many libraries to get things done.

Many of these are my takes on ideas that aren't new - and yet those ideas have to be rewritten everywhere we develop... and I find that a shame. Many ideas in here are of my own innovation! The unique difference is that everything is in support of how I like to develop, and I hope you will enjoy doing things this way too!

## Licensing
This software uses an MIT License, see LICENSE - terms and conditions contained therin.

## Thanks
As a matter of attribution, several of these utilities I have refined some with a friend, Ian Good. Thanks Ian!

## Final Message
I hope you are able to find ajut.ux.winui useful!

Thanks!
-AJ