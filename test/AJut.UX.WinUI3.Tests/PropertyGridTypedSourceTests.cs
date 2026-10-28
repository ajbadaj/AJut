namespace AJut.UX.WinUI3.Tests
{
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The generic source entry points are what keep a shown type's members in an app that trims, and only through the
    /// annotation on their type parameter, which nothing else would notice going missing. Checked without making the
    /// control.
    /// </summary>
    [TestClass]
    public class PropertyGridTypedSourceTests
    {
        [TestMethod]
        public void PropertyGrid_TypedSourceEntryPoints_KeepEveryMemberOfTheirType ()
        {
            foreach (string name in new[] { nameof(PropertyGrid.SetSingleItemSource), nameof(PropertyGrid.SetItemsSource) })
            {
                MethodInfo method = typeof(PropertyGrid).GetMethod(name);
                Assert.IsNotNull(method, $"PropertyGrid has no {name}");

                var keep = method.GetGenericArguments()[0].GetCustomAttribute<DynamicallyAccessedMembersAttribute>();
                Assert.AreEqual(DynamicallyAccessedMemberTypes.All, keep?.MemberTypes, $"{name}'s type parameter doesn't keep its type's members");
            }
        }
    }
}
