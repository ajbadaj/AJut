namespace AJut.Application.SourceControl
{
    using Text.AJson;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [Serializable]
    internal class WorkspaceToChekcoutCache
    {
        private string m_cachePath;
        public string WorkspaceName { get; set; }
        public List<string> ToCheckoutPaths { get; set; }
        public List<string> ToAddPaths { get; set; }

        public WorkspaceToChekcoutCache()
        {
            this.ToCheckoutPaths = new List<string>();
            this.ToAddPaths = new List<string>();
        }

        /*
        private static readonly XmlSerializer g_xmlSerializer = new XmlSerializer(typeof(WorkspaceToChekcoutCache));
        */

        public static WorkspaceToChekcoutCache Read(string cacheLocation, string workspaceName)
        {
            string filePath = Path.Combine(cacheLocation, workspaceName + ".p4cache");

            WorkspaceToChekcoutCache cacheFile = null;
            try
            {
                if (File.Exists(filePath))
                {
                    cacheFile = JsonHelper.BuildObjectForJson<WorkspaceToChekcoutCache>(JsonHelper.ParseFile(filePath));
                    /*
                    using (Stream s = File.OpenRead(filePath))
                    {
                        StreamReader reader = new StreamReader(s);
                        
                        cacheFile = g_xmlSerializer.Deserialize(reader) as WorkspaceToChekcoutCache;
                    }
                    */
                }
            }
            catch (Exception exc)
            {
                Logger.LogError("Failed to load p4cache info", exc);
            }

            if (cacheFile == null)
            {
                return new WorkspaceToChekcoutCache()
                {
                    m_cachePath = filePath
                };
            }
            else
            {
                cacheFile.m_cachePath = filePath;
                return cacheFile;
            }
        }

        public void Write()
        {
            try
            {
                /*
                using (Stream s = File.OpenWrite(m_cachePath))
                {
                    StreamWriter writer = new StreamWriter(s);
                    g_xmlSerializer.Serialize(writer, this);
                }
                */
                Json json = JsonHelper.BuildJsonForObject(this);

                if (!json.HasErrors)
                {
                    File.WriteAllText(m_cachePath, json.Data.StringValue);
                }
            }
            catch (Exception exc)
            {
                Logger.LogError("There was an error writing PerforceCmdManager's checkout later cache to disk.", exc);
            }

        }
    }
}
