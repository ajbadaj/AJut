namespace AJut.UX.Theming
{
    using Microsoft.UI.Xaml;
    using System.Linq;

    public static class ThemeHelpers
    {
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

        private static bool TryFindThemedResource(FrameworkElement currentPointOfOrigin, string fallbackTheme, object resourceKey, out object foundResource)
        {
            string theme = currentPointOfOrigin?.ActualTheme.ToString() ?? fallbackTheme;

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
