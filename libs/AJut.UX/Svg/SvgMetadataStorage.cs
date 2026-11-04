namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;

    public abstract class SvgMetadataStorage
    {
        protected readonly Dictionary<string, string> m_arbitraryData = new Dictionary<string, string>();
        public string this[string metadataKey] => m_arbitraryData[metadataKey];
        public Dictionary<string, string>.KeyCollection Keys => m_arbitraryData.Keys;
        public Dictionary<string, string>.ValueCollection Values => m_arbitraryData.Values;
        public int ArbitraryDataCount => m_arbitraryData.Count;
        public bool TryGetArbitraryData (string metadataKey, [MaybeNullWhen(false)] out string value)
        {
            return m_arbitraryData.TryGetValue(metadataKey, out value);
        }

        /// <summary>Adds a value, throwing if <paramref name="key"/> already has one</summary>
        public virtual void Add (string key, string value)
        {
            m_arbitraryData.Add(this.ToStoredKey(key), value);
        }

        /// <summary>Adds a value unless <paramref name="key"/> already has one, which it keeps. True when it was added.</summary>
        public bool TryAdd (string key, string value)
        {
            return m_arbitraryData.TryAdd(this.ToStoredKey(key), value);
        }

        /// <summary>The key a value is kept under, for a storage that keeps keys differently from how they are given</summary>
        protected virtual string ToStoredKey (string key) => key;

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
        public SvgNamespaceMetadataStorage (string name, string? url = null)
        {
            this.Namespace = name;
            this.SourceUrl = url;
        }

        public string Namespace { get; }
        public string? SourceUrl { get; internal set; }

        /// <summary>A key given with this storage's namespace prefix (inkscape:label) is kept without it (label)</summary>
        protected override string ToStoredKey (string key)
        {
            string prefix = $"{this.Namespace}:";
            return key.StartsWith(prefix, StringComparison.Ordinal) ? key.Substring(prefix.Length) : key;
        }

        public SvgNamespaceMetadataStorage Duplicate ()
        {
            var duplicate = new SvgNamespaceMetadataStorage(this.Namespace, this.SourceUrl);
            this.SetBaselineDuplicateData(duplicate);
            return duplicate;
        }
    }
}
