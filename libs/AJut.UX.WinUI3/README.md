# AJut.UX.WinUI3
C# / WinUI3 / dotnet 8 utility library, created by AJ Badarni

## Greetings
I hope you enjoy using ajut.ux. AJut is short for AJ Utilities, (pronounced like the dipping sauce Au Jus). This ux library contains controls, converters, extensions, content navigation, theming helpers, dark and light themes, utilities - and much more. I've been building these up since I first started developing - through the many side projects I've done over the years. The goal was to provide a simple project reference (ajut.core + ajut.ux.wpf) that will allow you to build and innovate without having to start completely from scratch or to have to bring in many many libraries to get things done.

Many of these are my takes on ideas that aren't new - and yet those ideas have to be rewritten everywhere we develop... and I find that a shame. Many ideas in here are of my own innovation! The unique difference is that everything is in support of how I like to develop, and I hope you will enjoy doing things this way too!

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