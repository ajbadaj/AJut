namespace AJut.UX
{
    using System.Collections.Generic;
    using System.Diagnostics;

    public abstract class SvgMetadataStorage
    {
        protected readonly Dictionary<string, string> m_arbitraryData = new Dictionary<string, string>();
        public string this[string metadataKey] => m_arbitraryData[metadataKey];
        public Dictionary<string, string>.KeyCollection Keys => m_arbitraryData.Keys;
        public Dictionary<string, string>.ValueCollection Values => m_arbitraryData.Values;
        public int ArbitraryDataCount => m_arbitraryData.Count;
        public bool TryGetArbitraryData (string metadataKey, out string value)
        {
            return m_arbitraryData.TryGetValue(metadataKey, out value);
        }

        public virtual void Add (string key, string value)
        {
            m_arbitraryData.Add(key, value);
        }

        public void SetBaselineDuplicateData (SvgMetadataStorage duplicate)
        {
            foreach (KeyValuePair<string,string> kvp in m_arbitraryData)
            {
                duplicate.m_arbitraryData.Add(kvp.Key, kvp.Value);
            }
        }
    }

    public class SvgElementMetadataStorage : SvgMetadataStorage
    {
        public SvgElementMetadataStorage (string id)
        {
            this.Id = id;
        }

        public string Id { get; set; }

        public virtual SvgElementMetadataStorage Duplicate ()
        {
            var duplicate = new SvgElementMetadataStorage(this.Id);
            this.SetBaselineDuplicateData(duplicate);
            return duplicate;
        }
    }


    [DebuggerDisplay("xmlns:{Namespace}={SourceUrl}")]
    public class SvgNamespaceMetadataStorage : SvgMetadataStorage
    {
        public SvgNamespaceMetadataStorage (string name, string url = null)
        {
            this.Namespace = name;
            this.SourceUrl = url;
        }

        public string Namespace { get; }
        public string SourceUrl { get; internal set; }

        public override void Add (string key, string value)
        {
            if (key.StartsWith($"{this.Namespace}:"))
            {
                key = key.Substring(this.Namespace.Length + 2);
            }

            m_arbitraryData.Add(key, value);
        }

        public SvgNamespaceMetadataStorage Duplicate ()
        {
            var duplicate = new SvgNamespaceMetadataStorage(this.Namespace, this.SourceUrl);
            this.SetBaselineDuplicateData(duplicate);
            return duplicate;
        }
    }
}
