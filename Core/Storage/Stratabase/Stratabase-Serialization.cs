namespace AJut.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Text.AJson;

    public sealed partial class Stratabase
    {
        private Stratabase (StratabaseDataModel data)
        {
            this.OverrideLayerCount = data.OverrideLayers.Length;
            m_baselineStorageLayer = StratabaseDataModel.FromStratumData(this, data.BaselineData);
            m_overrideStorageLayers = data.OverrideLayers.Select(layer => StratabaseDataModel.FromStratumData(this, layer)).ToArray();
        }

        /// <summary>
        /// Constructs a <see cref="Stratabase"/> from the json representation of a stratabase
        /// </summary>
        public static Stratabase DeserializeFromJson (Json json)
        {
            var data = JsonHelper.BuildObjectForJson<StratabaseDataModel>(json);
            if (data == null)
            {
                return null;
            }

            return new Stratabase(data);
        }

        /// <summary>
        /// Constructs a <see cref="Json"/> instance that represents the <see cref="Stratabase"/>.
        /// </summary>
        public Json SerializeToJson () => this.SerializeToJson(true, -1);

        /// <summary>
        /// Constructs a <see cref="Json"/> instance that represents the <see cref="Stratabase"/>.
        /// </summary>
        public Json SerializeToJson (bool includeBaseline = true, params int[] overrideLayersToInclude)
        {
            var output = new StratabaseDataModel(this, includeBaseline, overrideLayersToInclude);
            return JsonHelper.BuildJsonForObject(output, StratabaseDataModel.kJsonSettings);
        }

        private class StratabaseDataModel
        {
            public static JsonBuilder.Settings kJsonSettings = new JsonBuilder.Settings
            {
                KeyValuePairValueTypeIdToWrite = eTypeIdInfo.Any
            };

            public Dictionary<Guid, Dictionary<string, object>> BaselineData { get; set; }
            public Dictionary<Guid, Dictionary<string, object>>[] OverrideLayers { get; set; }

            public StratabaseDataModel () { }
            public StratabaseDataModel (Stratabase sb, bool includeBaseline, int[] layersToInclude)
            {
                this.BaselineData = includeBaseline ? _StratumToSaveData(sb.m_baselineStorageLayer) : null;

                bool includeAllOverrideLayers = layersToInclude.Length == 1 && layersToInclude[0] == -1;
                this.OverrideLayers = new Dictionary<Guid, Dictionary<string, object>>[sb.OverrideLayerCount];
                for (int stratumIndex = 0; stratumIndex < sb.OverrideLayerCount; ++stratumIndex)
                {
                    this.OverrideLayers[stratumIndex] = (includeAllOverrideLayers || layersToInclude.Contains(stratumIndex))
                            ? _StratumToSaveData(sb.m_overrideStorageLayers[stratumIndex])
                            : null;
                }
                sb.m_overrideStorageLayers.Select(_StratumToSaveData).ToArray();

                Dictionary<Guid, Dictionary<string, object>> _StratumToSaveData (Stratum _s)
                {
                    var output = new Dictionary<Guid, Dictionary<string, object>>();
                    foreach (KeyValuePair<Guid, PseudoPropertyBag> data in _s)
                    {
                        output.Add(data.Key, data.Value.m_storage);
                    }

                    return output;
                }
            }

            // This doesn't **need** to be here, but I like having it next to it's opposite above
            public static Stratum FromStratumData (Stratabase sb, Dictionary<Guid, Dictionary<string, object>> stratumData)
            {
                var output = new Stratum();
                if (stratumData != null)
                {
                    foreach (KeyValuePair<Guid, Dictionary<string, object>> data in stratumData)
                    {
                        output.Add(data.Key, new PseudoPropertyBag(sb, data.Value));
                    }
                }

                return output;
            }
        }
    }
}
