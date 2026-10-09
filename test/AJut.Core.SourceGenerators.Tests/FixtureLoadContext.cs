namespace AJut.Text.AJson.SourceGenerators.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Loader;

    /// <summary>
    /// Holds fixture assemblies that reference each other. Anything it was not handed (AJut.Core, the runtime) falls through to
    /// the default context, so fixtures share the AJut.Core the tests themselves run against.
    /// </summary>
    internal sealed class FixtureLoadContext : AssemblyLoadContext
    {
        private readonly Dictionary<string, Assembly> m_loaded = new Dictionary<string, Assembly>();

        public Assembly LoadImage (byte[] image)
        {
            Assembly loaded = this.LoadFromStream(new MemoryStream(image));
            m_loaded[loaded.GetName().Name!] = loaded;
            return loaded;
        }

        protected override Assembly? Load (AssemblyName assemblyName)
        {
            return assemblyName.Name != null && m_loaded.TryGetValue(assemblyName.Name, out Assembly? found) ? found : null;
        }
    }
}
