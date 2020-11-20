// TODO: Replace guts with tested TreeTraversal utilties from Core (which was based on this)
// TODO: When doing above-mentioned refactor, consier how to change things in a way considerate to UWP which does not have a logical tree.
namespace AJut.Application
{
    using AJut.Tree;
    using System;
    using System.Collections.Generic;
    using System.Linq;

#if WINDOWS_UWP
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Media.Media3D;
    using Visual = Windows.UI.Xaml.UIElement;

#else
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Media3D;
#endif
    /*
    Examples:
    Grid g;
    g.GetFirstParentOfType<Expander>();
    */


    public static class DependencyObjectXT
    {
        public delegate IEnumerable<DependencyObject> ChildrenSelector (DependencyObject target);
        public delegate DependencyObject ParentSelector (DependencyObject target);

        /// <summary>
        /// Checks if a dependency property has a local override
        /// </summary>
        public static bool IsSetLocally (this DependencyObject This, DependencyProperty property) => This.ReadLocalValue(property) != DependencyProperty.UnsetValue;

        public static bool IsVisual (this DependencyObject This)
        {
#if WINDOWS_UWP
            return This is Visual;
#else
            return This is Visual || This is Visual3D;
#endif
        }

        public static T GetFirstChildOf<T> (this DependencyObject start, eTraversalTree tree = eTraversalTree.Visual) where T : DependencyObject
        {
            var childSelector = ChildSelectorForTree(tree);
            return TreeTraversal<DependencyObject>.GetFirstChildOfType<T>(start, getChildrenMethodOverride: _=>childSelector(_));
        }

        public static T GetFirstParentOf<T> (this DependencyObject start, eTraversalTree tree = eTraversalTree.Visual) where T : DependencyObject
        {
            var parentSelector = ParentSelectorForTree(tree);
            var childSelector = ChildSelectorForTree(tree);
            return TreeTraversal<DependencyObject>.GetFirstParentOfType<T>(start, getParentMethodOverride: _ => parentSelector(_), getChildrenMethodOverride: _=>childSelector(_));
        }

        public static ChildrenSelector ChildSelectorForTree(eTraversalTree targetTree)
        {
            switch (targetTree)
            {
                case eTraversalTree.Visual:  return GetVisualChildren;
                case eTraversalTree.Logical: return GetLogicalChildren;
                default:                     return GetVisualAndLogicalChildren;
            }
        }

        public static IEnumerable<DependencyObject> GetLogicalChildren (this DependencyObject target)
        {
            return LogicalTreeHelper.GetChildren(target).OfType<DependencyObject>();
        }

        public static IEnumerable<DependencyObject> GetVisualChildren (this DependencyObject target)
        {
            int count = VisualTreeHelper.GetChildrenCount(target);
            for (int index = 0; index < count; ++index)
            {
                yield return VisualTreeHelper.GetChild(target, index);
            }
        }

        public static IEnumerable<DependencyObject> GetVisualAndLogicalChildren(this DependencyObject target)
        {
            var visited = new HashSet<DependencyObject>();
            foreach(DependencyObject child in target.GetVisualChildren().Concat(target.GetLogicalChildren()))
            {
                if (visited.Add(child))
                {
                    yield return child;
                }
            }
        }

        public static ParentSelector ParentSelectorForTree (eTraversalTree targetTree)
        {
            switch (targetTree)
            {
                case eTraversalTree.Visual: return GetVisualParent;
                case eTraversalTree.Logical: return GetLogicalParent;
                default: return _=>GetVisualParent(_) ?? GetLogicalParent(_);
            }
        }

        public static DependencyObject GetLogicalParent (this DependencyObject target)
        {
            return LogicalTreeHelper.GetParent(target);
        }

        public static DependencyObject GetVisualParent (this DependencyObject target)
        {
            return VisualTreeHelper.GetParent(target);
        }
    }

    // --------- Public Support Components ----------
    [Flags]
    public enum eTraversalTree
    {
        Logical = 0b01,
        Visual  = 0b10,
        Both = Logical | Visual
    };
}
