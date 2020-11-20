namespace AJut.Application
{
    using System.ComponentModel;
    using System.Windows;

    public static class IDEHelper
    {
        private static bool? g_bIsInDesignMode = null;

        public static bool IsInDesignMode
        {
            get
            {
                if (g_bIsInDesignMode == null)
                {
                    g_bIsInDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
                }
                return g_bIsInDesignMode.Value;
            }
        }
    }
}
