namespace AJut.Core.UnitTests
{
    using System.IO;
    using System.Reflection;

    public static class ResourceFetcher
    {
        /// <summary>
        /// Returns a stream for the given resource
        /// </summary>
        /// <param name="resourceName">A path to the resource in 'dir/name' format</param>
        public static Stream Get(string resourceName)
        {
            string fullResourceName = string.Format("{0}.{1}", Assembly.GetCallingAssembly().GetName().Name, resourceName.Replace('/', '.'));
            return typeof(ResourceFetcher).Assembly.GetManifestResourceStream(fullResourceName);
        }

        /// <summary>
        /// Returns all the text for a given resource file
        /// </summary>
        /// <param name="resourceName">A path to the resource in 'dir/name' format</param>
        public static string GetText(string resourceName)
        {
            using (Stream stream = Get(resourceName))
            {
                var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }
    }
}
