namespace AJut.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.TypeManagement;

    public sealed partial class Stratabase
    {
        // ===========================[ Public Methods ]=======================================

        // ----------------- Enumerate Layer Entries -----------------

        /// <summary>
        /// Enumerates every value stored in the baseline layer, one <see cref="StratumEntry"/> per object id and property. This is a
        /// live view: values are the stored instances, not copies, and the <see cref="Stratabase"/> must not be modified while
        /// enumerating it (the same rule as enumerating any Dictionary). Use <see cref="ExportBaseline"/> for a detached snapshot.
        /// </summary>
        public IEnumerable<StratumEntry> GetAllBaselineEntries () => EnumerateEntries(m_baselineStorageLayer);

        /// <summary>
        /// Enumerates every value stored in one override layer, one <see cref="StratumEntry"/> per object id and property. This is a
        /// live view: values are the stored instances, not copies, and the <see cref="Stratabase"/> must not be modified while
        /// enumerating it (the same rule as enumerating any Dictionary). Use <see cref="ExportOverrideLayer"/> for a detached snapshot.
        /// </summary>
        public IEnumerable<StratumEntry> GetAllOverrideEntries (int layer)
        {
            // Checked here rather than inside the iterator, so a bad layer throws at the call and not at the first MoveNext
            this.ValidateOverrideLayerIndex(layer);
            return EnumerateEntries(m_overrideStorageLayers[layer]);
        }

        // ----------------- Export -----------------

        /// <summary>
        /// Takes a detached snapshot of every value stored in the baseline layer, shaped for <see cref="ImportIntoBaseline"/>. The type id
        /// entries (<see cref="kTypeIdStorage"/>) are included, so a round trip keeps them. List values are copied, since the store
        /// edits its lists in place; every other value is the stored instance, the same one the property getters hand out.
        /// </summary>
        public StratumEntry[] ExportBaseline () => ExportEntries(m_baselineStorageLayer);

        /// <summary>
        /// Takes a detached snapshot of every value stored in one override layer, shaped for <see cref="ImportIntoOverrideLayer"/>.
        /// List values are copied, since the store edits its lists in place; every other value is the stored instance, the same one
        /// the property getters hand out.
        /// </summary>
        public StratumEntry[] ExportOverrideLayer (int layer)
        {
            this.ValidateOverrideLayerIndex(layer);
            return ExportEntries(m_overrideStorageLayers[layer]);
        }

        // ----------------- Import -----------------

        /// <summary>
        /// Writes <paramref name="entries"/> into the baseline layer of this existing <see cref="Stratabase"/>. Works the same way as
        /// <see cref="ImportIntoOverrideLayer"/>; see its remarks for what <paramref name="notifyOfChanges"/> does and does not silence.
        /// </summary>
        /// <param name="entries">The values to write, usually from <see cref="ExportBaseline"/>. If two entries name the same property, the last one wins.</param>
        /// <param name="mode">Whether values the import does not name stay (<see cref="eStratumImportMode.Merge"/>) or are removed (<see cref="eStratumImportMode.Replace"/>)</param>
        /// <param name="notifyOfChanges">When false, <see cref="BaselineDataChanged"/> is not raised for the import's writes. Property access objects still track them.</param>
        /// <returns>How many values actually changed, set or removed. Zero means the layer already held exactly what was imported.</returns>
        /// <exception cref="ArgumentException">An entry has no property name. Thrown before anything is written.</exception>
        public int ImportIntoBaseline (IEnumerable<StratumEntry> entries, eStratumImportMode mode = eStratumImportMode.Merge, bool notifyOfChanges = true)
        {
            return this.ImportEntries(kActiveLayerBaseline, entries, mode, notifyOfChanges);
        }

        /// <summary>
        /// Writes <paramref name="entries"/> into one override layer of this existing <see cref="Stratabase"/>. Each value goes through the
        /// same path as <see cref="SetOverridePropertyValue"/>, so an equal value is skipped and raises nothing. Lists are compared element
        /// by element and copied on the way in, so an equal list raises nothing either, and two stores importing one export never share a list.
        /// </summary>
        /// <param name="layer">The override layer to write into</param>
        /// <param name="entries">The values to write, usually from <see cref="ExportOverrideLayer"/>. If two entries name the same property, the last one wins.</param>
        /// <param name="mode">Whether values the import does not name stay (<see cref="eStratumImportMode.Merge"/>) or are removed (<see cref="eStratumImportMode.Replace"/>)</param>
        /// <param name="notifyOfChanges">When false, <see cref="OverrideDataChanged"/> is not raised for the import's writes. Property access objects still track them (see remarks).</param>
        /// <returns>How many values actually changed, set or removed. Zero means the layer already held exactly what was imported.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="layer"/> is not one of this store's override layers</exception>
        /// <exception cref="ArgumentException">An entry has no property name. Thrown before anything is written.</exception>
        /// <remarks>
        /// <paramref name="notifyOfChanges"/> false keeps the store wide <see cref="BaselineDataChanged"/> and <see cref="OverrideDataChanged"/>
        /// quiet, so applying a snapshot or a restore does not look like a run of edits to whatever listens to the store. It does NOT keep the
        /// property access objects (<see cref="StrataPropertyValueAccess{TProperty}"/>, <see cref="StrataPropertyListAccess{TElement}"/>,
        /// <see cref="StratabaseBackedModel"/>) out of it: they still see every change and raise their own ValueChanged. That is deliberate.
        /// They cache whether a property is set and which layer is active, so an import that went around them would leave a property imported
        /// into an unset or higher layer reading its old value, with nothing anywhere to say so. A stale value nobody can see is a worse bug than
        /// an event nobody expected.
        /// <para>
        /// This is not how <see cref="ClearAll(bool)"/> treats notifyOfRemovals false: that skips the access objects as well.
        /// </para>
        /// <para>
        /// The silence covers only the import's own writes. Anything a handler writes while the import is running notifies normally.
        /// </para>
        /// </remarks>
        public int ImportIntoOverrideLayer (int layer, IEnumerable<StratumEntry> entries, eStratumImportMode mode = eStratumImportMode.Merge, bool notifyOfChanges = true)
        {
            this.ValidateOverrideLayerIndex(layer);
            return this.ImportEntries(layer, entries, mode, notifyOfChanges);
        }

        // ===========================[ Utility Methods ]======================================

        private void ValidateOverrideLayerIndex (int layer)
        {
            if (layer < 0 || layer >= this.OverrideLayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(layer), layer, $"Override layer {layer} does not exist, this {nameof(Stratabase)} has {this.OverrideLayerCount} override layers");
            }
        }

        private static IEnumerable<StratumEntry> EnumerateEntries (Stratum stratum)
        {
            foreach (KeyValuePair<Guid, PseudoPropertyBag> idAndBag in stratum)
            {
                foreach (KeyValuePair<string, object> propertyAndValue in idAndBag.Value.m_storage)
                {
                    yield return new StratumEntry(idAndBag.Key, propertyAndValue.Key, propertyAndValue.Value);
                }
            }
        }

        private static StratumEntry[] ExportEntries (Stratum stratum)
        {
            // Count first so the snapshot is one allocation of exactly the right size
            int entryCount = 0;
            foreach (PseudoPropertyBag propertyBag in stratum.Values)
            {
                entryCount += propertyBag.m_storage.Count;
            }

            var output = new StratumEntry[entryCount];
            int index = 0;
            foreach (KeyValuePair<Guid, PseudoPropertyBag> idAndBag in stratum)
            {
                foreach (KeyValuePair<string, object> propertyAndValue in idAndBag.Value.m_storage)
                {
                    output[index++] = new StratumEntry(idAndBag.Key, propertyAndValue.Key, DetachStoreEditedValue(propertyAndValue.Value));
                }
            }

            return output;
        }

        private int ImportEntries (int layer, IEnumerable<StratumEntry> entries, eStratumImportMode mode, bool notifyOfChanges)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            // 1. Take every entry and check it before the first write, so a bad entry throws without leaving the layer half imported.
            //  Taking them up front also makes it safe to import a live enumeration of this same store.
            StratumEntry[] toImport = entries as StratumEntry[] ?? entries.ToArray();
            for (int index = 0; index < toImport.Length; ++index)
            {
                if (String.IsNullOrEmpty(toImport[index].PropertyName))
                {
                    throw new ArgumentException($"Entry {index} (item {toImport[index].ItemId}) has no property name. An empty property name is reserved: removal events use it to mean every property of an item was cleared.", nameof(entries));
                }
            }

            bool isBaseline = layer == kActiveLayerBaseline;
            Stratum stratum = isBaseline ? m_baselineStorageLayer : m_overrideStorageLayers[layer];
            int changeCount = 0;

            // 2. Replace: remove what the layer holds that the import does not name
            if (mode == eStratumImportMode.Replace)
            {
                foreach (StratumEntry unnamed in FindEntriesNotNamedBy(stratum, toImport))
                {
                    ObjectDataAccessManager odam = this.EnsureDataAccess(unnamed.ItemId);
                    bool wasRemoved;
                    if (isBaseline)
                    {
                        wasRemoved = odam.ObliteratePropertyStorageInBaseline(unnamed.PropertyName, notifyOfChanges);
                    }
                    else
                    {
                        wasRemoved = odam.ObliteratePropertyStorageInLayer(layer, unnamed.PropertyName, notifyOfChanges);
                    }

                    if (wasRemoved)
                    {
                        ++changeCount;
                    }
                }
            }

            // 3. Set each entry. Equal values are skipped first, so an import that changes nothing raises nothing whatever notifyOfChanges says.
            foreach (StratumEntry entry in toImport)
            {
                if (stratum.TryGetValue(entry.ItemId, out PseudoPropertyBag propertyBag)
                    && propertyBag.TryGetValue(entry.PropertyName, out object storedValue)
                    && this.AreEqualForImport(storedValue, entry.Value))
                {
                    continue;
                }

                object value = DetachStoreEditedValue(entry.Value);
                ObjectDataAccessManager odam = this.EnsureDataAccess(entry.ItemId);
                bool wasSet = isBaseline
                    ? odam.SetBaselineValue(entry.PropertyName, value, notifyOfChanges)
                    : odam.SetOverrideValue(layer, entry.PropertyName, value, notifyOfChanges);

                if (wasSet)
                {
                    ++changeCount;
                }
            }

            return changeCount;
        }

        /// <summary>
        /// Finds what <paramref name="stratum"/> holds that <paramref name="entries"/> does not name, as a list built up front so the
        /// caller can remove them without editing the stratum while it is being enumerated
        /// </summary>
        private static List<StratumEntry> FindEntriesNotNamedBy (Stratum stratum, StratumEntry[] entries)
        {
            var namedPropertiesById = new Dictionary<Guid, HashSet<string>>();
            foreach (StratumEntry entry in entries)
            {
                if (!namedPropertiesById.TryGetValue(entry.ItemId, out HashSet<string> namedProperties))
                {
                    namedProperties = new HashSet<string>();
                    namedPropertiesById.Add(entry.ItemId, namedProperties);
                }

                namedProperties.Add(entry.PropertyName);
            }

            var output = new List<StratumEntry>();
            foreach (KeyValuePair<Guid, PseudoPropertyBag> idAndBag in stratum)
            {
                namedPropertiesById.TryGetValue(idAndBag.Key, out HashSet<string> namedProperties);
                foreach (KeyValuePair<string, object> propertyAndValue in idAndBag.Value.m_storage)
                {
                    if (namedProperties == null || !namedProperties.Contains(propertyAndValue.Key))
                    {
                        output.Add(new StratumEntry(idAndBag.Key, propertyAndValue.Key, propertyAndValue.Value));
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// The store's own <see cref="ValueEqualityTester"/>, plus the two cases it misses that an import meets all the time: the same
        /// instance (null included, which the default tester calls unequal), and a list of equal elements (the default tester compares
        /// lists by reference, and an export always carries a copy)
        /// </summary>
        private bool AreEqualForImport (object storedValue, object importedValue)
        {
            if (ReferenceEquals(storedValue, importedValue))
            {
                return true;
            }

            if (storedValue is IList storedList
                && importedValue is IList importedList
                && IsStoreEditedListType(storedValue.GetType())
                && (storedValue.GetType() == importedValue.GetType()))
            {
                if (storedList.Count != importedList.Count)
                {
                    return false;
                }

                for (int index = 0; index < storedList.Count; ++index)
                {
                    object storedElement = storedList[index];
                    object importedElement = importedList[index];
                    if (!ReferenceEquals(storedElement, importedElement) && !this.ValueEqualityTester(storedElement, importedElement))
                    {
                        return false;
                    }
                }

                return true;
            }

            return this.ValueEqualityTester(storedValue, importedValue);
        }

        /// <summary>
        /// The store builds its lists as List&lt;T&gt; and inserts into and removes from them in place, so a list that crosses the store
        /// boundary is copied: an export must not change when the store does, and two stores importing one export must not end up sharing
        /// a list. Any other value crosses as the same instance, the way every getter hands it out.
        /// </summary>
        private static object DetachStoreEditedValue (object value)
        {
            if (value is IList list && IsStoreEditedListType(value.GetType()))
            {
                var copy = (IList)AJutActivator.CreateInstanceOf(value.GetType());
                for (int index = 0; index < list.Count; ++index)
                {
                    copy.Add(list[index]);
                }

                return copy;
            }

            return value;
        }

        private static bool IsStoreEditedListType (Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
    }
}
