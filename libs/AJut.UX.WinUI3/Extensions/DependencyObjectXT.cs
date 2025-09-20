namespace AJut.UX
{
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Tree;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;

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

        public static T GetFirstChildOf<T> (this DependencyObject start)
        {
            return TreeTraversal<DependencyObject>.GetFirstChildOfType<T>(start, getChildrenMethodOverride: _=> GetVisualChildren(_));
        }

        public static T GetFirstParentOf<T> (this DependencyObject start) where T : DependencyObject
        {
            return TreeTraversal<DependencyObject>.GetFirstParentOfType<T>(start, getParentMethodOverride: _ => GetVisualParent(_), getChildrenMethodOverride: _=> GetVisualChildren(_));
        }

        public static IEnumerable<T> SearchParentsOf<T> (this DependencyObject start, bool includeSelf = false)
        {
            return TreeTraversal<DependencyObject>.All(start, eTraversalFlowDirection.ThroughParents, includeSelf:includeSelf, getParentMethodOverride: _ => GetVisualParent(_), getChildrenMethodOverride: _ => GetVisualChildren(_)).OfType<T>();
        }

        public static IEnumerable<DependencyObject> GetVisualChildren (this DependencyObject target)
        {
            int count = VisualTreeHelper.GetChildrenCount(target);
            for (int index = 0; index < count; ++index)
            {
                yield return VisualTreeHelper.GetChild(target, index);
            }
        }

        public static DependencyObject GetVisualParent (this DependencyObject target)
        {
            return VisualTreeHelper.GetParent(target);
        }
    }
}
