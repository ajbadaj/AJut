namespace AJut.Application
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The collection of all ancillary svg data
    /// </summary>
    public class SvgMetadata : SvgElementMetadataStorage
    {
        private readonly List<SvgNamespaceMetadataStorage> m_namespaceMetadataStorage = new List<SvgNamespaceMetadataStorage>();
        private readonly Dictionary<string,SvgElementMetadataStorage> m_idMetadataStorage = new Dictionary<string, SvgElementMetadataStorage>();

        public SvgMetadata (string id, double width = double.NaN, double height = double.NaN) : base(id ?? string.Empty)
        {
            this.Width = width;
            this.Height = height;
        }

        public double Width { get; private set; }
        public double Height { get; private set; }

        public SvgNamespaceMetadataStorage GetOrGenerateNamespace (string name, string url = null)
        {
            SvgNamespaceMetadataStorage existing = m_namespaceMetadataStorage.FirstOrDefault(n => n.Namespace == name);
            if (existing != null)
            {
                if (existing.SourceUrl == null)
                {
                    existing.SourceUrl = url;
                }
            }
            else
            {
                existing = new SvgNamespaceMetadataStorage(name, url);
                m_namespaceMetadataStorage.Add(existing);
            }

            return existing;
        }

        public SvgElementMetadataStorage GetOrGenerateElementMetadata (string id)
        {
            if (!m_idMetadataStorage.TryGetValue(id, out SvgElementMetadataStorage existing))
            {
                existing = new SvgElementMetadataStorage(id);
                m_idMetadataStorage.Add(id, existing);
            }

            return existing;
        }

        public void AddMetadata (string key, string value)
        {
            int namespaceSplit = key.IndexOf(':');
            if (namespaceSplit != -1)
            {
                string metaNamespace = key.Substring(0, namespaceSplit);
                this.GetOrGenerateNamespace(metaNamespace).Add(key.Substring(namespaceSplit + 1), value);
            }

            m_arbitraryData.TryAdd(key, value);
        }

        public void AddMetadata (string storeId, string key, string value)
        {
            if (storeId.IsNullOrEmpty())
            {
                return;
            }

            int namespaceSplit = key.IndexOf(':');
            if (namespaceSplit != -1)
            {
                string metaNamespace = key.Substring(0, namespaceSplit);
                this.GetOrGenerateNamespace(metaNamespace).Add($"{storeId}.{key.Substring(namespaceSplit + 1)}", value);
            }

            this.GetOrGenerateElementMetadata(storeId).Add(key, value);
        }

        public override SvgMetadata Duplicate ()
        {
            var duplicate = new SvgMetadata(this.Id, this.Width, this.Height);
            duplicate.m_namespaceMetadataStorage.AddRange(m_namespaceMetadataStorage);
            foreach (KeyValuePair<string,SvgElementMetadataStorage> kvp in m_idMetadataStorage)
            {
                duplicate.m_idMetadataStorage.Add(kvp.Key, kvp.Value.Duplicate());
            }

            this.SetBaselineDuplicateData(duplicate);
            return duplicate;
        }
    }
}
