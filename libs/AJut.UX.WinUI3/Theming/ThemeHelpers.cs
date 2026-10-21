namespace AJut.UX.Theming
{
    using Microsoft.UI.Xaml;
    using System.Linq;

    public static class ThemeHelpers
    {
        /// <summary>
        /// Looks up a resource the way a ThemeResource reference on <paramref name="pointOfOrigin"/> would: through its resources
        /// and its visual parents', then the application's, reading theme dictionaries for the point of origin's
        /// <see cref="FrameworkElement.ActualTheme"/> the whole way up.
        /// </summary>
        public static bool TryFindThemedResource(this FrameworkElement pointOfOrigin, object resourceKey, out object foundResource)
        {
            return TryFindThemedResource(pointOfOrigin, pointOfOrigin.ActualTheme.ToString(), resourceKey, out foundResource);
        }

        public static bool TryFindThemedResource(string theme, object resourceKey, out object foundResource)
        {
            return TryFindThemedResource(null, theme, resourceKey, out foundResource);
        }

        public static bool TryFindThemedResource(object resourceKey, out object foundResource)
        {
            return TryFindThemedResource(null, Application.Current.RequestedTheme.ToString(), resourceKey, out foundResource);
        }

        private static bool TryFindThemedResource(FrameworkElement currentPointOfOrigin, string theme, object resourceKey, out object foundResource)
        {
            // The theme is the original point of origin's for the whole walk, never re-read from the parents. That's how a
            // ThemeResource reference resolves: the element's ActualTheme (the nearest RequestedTheme above it, else the app's)
            // picks the theme dictionary in every resource dictionary on the way up, the application's included. See
            // https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.frameworkelement.requestedtheme
            //
            // The parents can be on a different theme than the element. A window's content is the usual case: AppThemeManager
            // sets RequestedTheme on the content and on every parent it has at that moment, and at startup the content has no
            // parent yet, so the window's own root elements above it keep following the app's theme, which is the system's.
            // Reading the theme off each parent gave a window switched away from the system theme the system theme's values
            // as soon as the walk got past the content.

            // If point of origin is null, then we've reached the top and all there is left to do is check Application
            if (currentPointOfOrigin == null)
            {
                foundResource = _RecursivelySearchResourceDictionary(Application.Current.Resources);
                return foundResource != null;
            }
            else
            {
                foundResource = _RecursivelySearchResourceDictionary(currentPointOfOrigin.Resources);
                if (foundResource != null)
                {
                    return true;
                }

                return TryFindThemedResource(currentPointOfOrigin.GetFirstParentOf<FrameworkElement>(), theme, resourceKey, out foundResource);
            }

            object _RecursivelySearchResourceDictionary(ResourceDictionary root)
            {
                // First search the merged dictionaries, bottom to top
                if (root.MergedDictionaries?.Count > 0)
                {
                    foreach (ResourceDictionary dictionary in root.MergedDictionaries.Reverse())
                    {
                        object found = _RecursivelySearchResourceDictionary(dictionary);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                }

                // If we haven't found it there, then check the themed dictionaries
                if (root.ThemeDictionaries.TryGetValue(theme, out var themeDict) && themeDict is ResourceDictionary themedResourceDictionary)
                {
                    if (themedResourceDictionary.TryGetValue(resourceKey, out object found))
                    {
                        return found;
                    }
                }

                // Fallback to "Default"
                if (root.ThemeDictionaries.TryGetValue("Default", out var defaultDict) && defaultDict is ResourceDictionary defaultResourceDictionary)
                {
                    if (defaultResourceDictionary.TryGetValue(resourceKey, out object found))
                    {
                        return found;
                    }
                }

                // Now check in the dictionary itself
                return _GetValueOrNull(root);

                // Not using TryGetValue because it does some of it's own weird resourcing that isn't getting the themed results properly...
                object _GetValueOrNull(ResourceDictionary target)
                {
                    return target.Keys.Where(o => o.Equals(resourceKey)).Any() ? target[resourceKey] : null;
                }
            }
        }
    }
}
