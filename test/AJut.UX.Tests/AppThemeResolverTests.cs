#nullable enable

namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Theming;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AppThemeResolverTests
    {
        // ===[ Starting up ]===

        [TestMethod]
        public void ATR_NoStore_StartsFromFallback_ResolvingTheOS ()
        {
            var os = new TestOSSource(eAppTheme.Light);
            var resolver = new AppThemeResolver(os, store: null, fallback: eAppThemeConfiguration.UseSameAsOS);
            var themes = Track(resolver);

            Assert.AreEqual(eAppThemeConfiguration.UseSameAsOS, resolver.ThemeConfiguration);
            Assert.AreEqual(eAppTheme.Light, resolver.CurrentTheme);
            Assert.AreEqual(0, themes.Count, "Nothing is raised while starting up");
        }

        [TestMethod]
        public void ATR_SavedChoice_WinsOverFallback ()
        {
            var store = new TestStore(eAppThemeConfiguration.Light);
            var resolver = new AppThemeResolver(new TestOSSource(eAppTheme.Dark), store, fallback: eAppThemeConfiguration.Dark);

            Assert.AreEqual(eAppThemeConfiguration.Light, resolver.ThemeConfiguration);
            Assert.AreEqual(eAppTheme.Light, resolver.CurrentTheme);
            Assert.AreEqual(0, store.Saved.Count, "Loading does not save");
        }

        [TestMethod]
        public void ATR_EmptyStore_StartsFromFallback ()
        {
            var resolver = new AppThemeResolver(new TestOSSource(eAppTheme.Light), new TestStore(null), fallback: eAppThemeConfiguration.Dark);

            Assert.AreEqual(eAppThemeConfiguration.Dark, resolver.ThemeConfiguration);
            Assert.AreEqual(eAppTheme.Dark, resolver.CurrentTheme);
        }

        // ===[ Changing the choice ]===

        [TestMethod]
        public void ATR_NewChoice_IsSaved_AndMovesTheTheme ()
        {
            var store = new TestStore(null);
            var resolver = new AppThemeResolver(new TestOSSource(eAppTheme.Light), store, eAppThemeConfiguration.UseSameAsOS);
            var themes = Track(resolver);
            var changed = TrackProperties(resolver);

            resolver.ThemeConfiguration = eAppThemeConfiguration.Dark;

            CollectionAssert.AreEqual(new[] { eAppThemeConfiguration.Dark }, store.Saved);
            Assert.AreEqual(eAppTheme.Dark, resolver.CurrentTheme);
            CollectionAssert.AreEqual(new[] { eAppTheme.Dark }, themes);
            CollectionAssert.Contains(changed, nameof(AppThemeResolver.ThemeConfiguration));
            CollectionAssert.Contains(changed, nameof(AppThemeResolver.CurrentTheme));
        }

        [TestMethod]
        public void ATR_NewChoice_IsSettled_BeforeItIsRaised ()
        {
            var resolver = new AppThemeResolver(new TestOSSource(eAppTheme.Light), null, eAppThemeConfiguration.Light);
            eAppTheme? themeSeenWithTheChoice = null;
            resolver.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(AppThemeResolver.ThemeConfiguration))
                {
                    themeSeenWithTheChoice = resolver.CurrentTheme;
                }
            };

            resolver.ThemeConfiguration = eAppThemeConfiguration.Dark;

            Assert.AreEqual(eAppTheme.Dark, themeSeenWithTheChoice, "A listener to the choice already reads the theme it leads to");
        }

        [TestMethod]
        public void ATR_NewChoice_ThatKeepsTheTheme_IsSaved_ButRaisesNoThemeChange ()
        {
            var store = new TestStore(null);
            var resolver = new AppThemeResolver(new TestOSSource(eAppTheme.Light), store, eAppThemeConfiguration.UseSameAsOS);
            var themes = Track(resolver);

            resolver.ThemeConfiguration = eAppThemeConfiguration.Light;

            CollectionAssert.AreEqual(new[] { eAppThemeConfiguration.Light }, store.Saved);
            Assert.AreEqual(eAppTheme.Light, resolver.CurrentTheme);
            Assert.AreEqual(0, themes.Count);
        }

        [TestMethod]
        public void ATR_SameChoice_IsNotSavedAgain ()
        {
            var store = new TestStore(null);
            var resolver = new AppThemeResolver(new TestOSSource(eAppTheme.Light), store, eAppThemeConfiguration.Dark);

            resolver.ThemeConfiguration = eAppThemeConfiguration.Dark;

            Assert.AreEqual(0, store.Saved.Count);
        }

        [TestMethod]
        public void ATR_ChoosingUseSameAsOS_TakesTheOSTheme ()
        {
            var resolver = new AppThemeResolver(new TestOSSource(eAppTheme.Light), null, eAppThemeConfiguration.Dark);
            var themes = Track(resolver);

            resolver.ThemeConfiguration = eAppThemeConfiguration.UseSameAsOS;

            Assert.AreEqual(eAppTheme.Light, resolver.CurrentTheme);
            CollectionAssert.AreEqual(new[] { eAppTheme.Light }, themes);
        }

        // ===[ The OS changing ]===

        [TestMethod]
        public void ATR_OSChange_IsFollowed_UnderUseSameAsOS ()
        {
            var os = new TestOSSource(eAppTheme.Light);
            var resolver = new AppThemeResolver(os, null, eAppThemeConfiguration.UseSameAsOS);
            var themes = Track(resolver);

            os.Change(eAppTheme.Dark);

            Assert.AreEqual(eAppTheme.Dark, resolver.CurrentTheme);
            CollectionAssert.AreEqual(new[] { eAppTheme.Dark }, themes);
        }

        [TestMethod]
        public void ATR_OSChange_IsIgnored_UnderAChosenTheme ()
        {
            var os = new TestOSSource(eAppTheme.Light);
            var resolver = new AppThemeResolver(os, null, eAppThemeConfiguration.Light);
            var themes = Track(resolver);

            os.Change(eAppTheme.Dark);

            Assert.AreEqual(eAppTheme.Light, resolver.CurrentTheme);
            Assert.AreEqual(0, themes.Count);
        }

        [TestMethod]
        public void ATR_OSChange_ToTheSameTheme_RaisesNothing ()
        {
            var os = new TestOSSource(eAppTheme.Light);
            var resolver = new AppThemeResolver(os, null, eAppThemeConfiguration.UseSameAsOS);
            var themes = Track(resolver);

            os.Change(eAppTheme.Light);

            Assert.AreEqual(0, themes.Count);
        }

        // ===[ Forcing ]===

        [TestMethod]
        public void ATR_Force_RaisesEvenWhenNothingChanged_AndSaves ()
        {
            var store = new TestStore(null);
            var resolver = new AppThemeResolver(new TestOSSource(eAppTheme.Light), store, eAppThemeConfiguration.Dark);
            var themes = Track(resolver);
            var changed = TrackProperties(resolver);

            resolver.ForceThemeChange(eAppThemeConfiguration.Dark);

            CollectionAssert.AreEqual(new[] { eAppTheme.Dark }, themes);
            CollectionAssert.AreEqual(new[] { eAppThemeConfiguration.Dark }, store.Saved);
            CollectionAssert.Contains(changed, nameof(AppThemeResolver.ThemeConfiguration));
            CollectionAssert.DoesNotContain(changed, nameof(AppThemeResolver.CurrentTheme), "The theme in effect did not change");
        }

        [TestMethod]
        public void ATR_Force_ToANewChoice_MovesTheTheme ()
        {
            var resolver = new AppThemeResolver(new TestOSSource(eAppTheme.Dark), null, eAppThemeConfiguration.Light);
            var themes = Track(resolver);

            resolver.ForceThemeChange(eAppThemeConfiguration.UseSameAsOS);

            Assert.AreEqual(eAppThemeConfiguration.UseSameAsOS, resolver.ThemeConfiguration);
            Assert.AreEqual(eAppTheme.Dark, resolver.CurrentTheme);
            CollectionAssert.AreEqual(new[] { eAppTheme.Dark }, themes);
        }

        // ===[ Teardown ]===

        [TestMethod]
        public void ATR_Dispose_StopsFollowingTheOS ()
        {
            var os = new TestOSSource(eAppTheme.Light);
            var resolver = new AppThemeResolver(os, null, eAppThemeConfiguration.UseSameAsOS);
            Assert.AreEqual(1, os.SubscriberCount);

            resolver.Dispose();
            resolver.Dispose();

            Assert.AreEqual(0, os.SubscriberCount);
            os.Change(eAppTheme.Dark);
            Assert.AreEqual(eAppTheme.Light, resolver.CurrentTheme);
        }

        // ===[ Helpers ]===

        private static List<eAppTheme> Track (AppThemeResolver resolver)
        {
            var themes = new List<eAppTheme>();
            resolver.ThemeChanged += (sender, e) => themes.Add(e.Value);
            return themes;
        }

        private static List<string> TrackProperties (AppThemeResolver resolver)
        {
            var names = new List<string>();
            resolver.PropertyChanged += (sender, e) => names.Add(e.PropertyName!);
            return names;
        }

        private sealed class TestOSSource : IOSThemeSource
        {
            private eAppTheme m_theme;
            private EventHandler? m_osThemeChanged;

            public TestOSSource (eAppTheme theme)
            {
                m_theme = theme;
            }

            public int SubscriberCount => m_osThemeChanged?.GetInvocationList().Length ?? 0;

            public event EventHandler OSThemeChanged
            {
                add => m_osThemeChanged += value;
                remove => m_osThemeChanged -= value;
            }

            public eAppTheme GetOSTheme () => m_theme;

            public void Change (eAppTheme theme)
            {
                m_theme = theme;
                m_osThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class TestStore : IAppThemeConfigurationStore
        {
            private readonly eAppThemeConfiguration? m_saved;

            public TestStore (eAppThemeConfiguration? saved)
            {
                m_saved = saved;
            }

            public List<eAppThemeConfiguration> Saved { get; } = new List<eAppThemeConfiguration>();

            public bool TryLoad (out eAppThemeConfiguration configuration)
            {
                configuration = m_saved ?? default;
                return m_saved.HasValue;
            }

            public void Save (eAppThemeConfiguration configuration) => this.Saved.Add(configuration);
        }
    }
}
