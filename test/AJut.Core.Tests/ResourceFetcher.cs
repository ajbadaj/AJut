namespace AJut.Core.UnitTests
{
    using System.IO;
    using System.Reflection;
    using AJut.IO;

    public static class ResourceFetcher
    {
        /// <summary>
        /// Returns a stream for the given resource
        /// </summary>
        /// <param name="resourceName">A path to the resource in 'dir/name' format</param>
        public static Stream Get(string resourceName)
        {
            return FileHelpers.GetEmbeddedResourceStream(resourceName);
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
