using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using CodePlex.TfsLibrary.Properties;
using CodePlex.TfsLibrary.Utility;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public class IgnoreList : IIgnoreList
    {
        readonly IFileSystem fileSystem;
        string ignoreFilename;

        public IgnoreList(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public string IgnoreFilename
        {
            get { return ignoreFilename; }
            set { ignoreFilename = value; }
        }

        IgnoreElement GetIgnoreElement(string directory)
        {
            try
            {
                string filename = fileSystem.CombinePath(directory, ignoreFilename);
                string xml = fileSystem.ReadAllText(filename);

                XmlSerializer ser = new XmlSerializer(typeof(IgnoreElement));

                using (StringReader xmlStringReader = new StringReader(xml))
                using (StringReader schemaStringReader = new StringReader(Resources.IgnoreListSchema))
                {
                    XmlSchema schema = XmlSchema.Read(schemaStringReader, null);
                    XmlReaderSettings settings = new XmlReaderSettings();
                    settings.ValidationType = ValidationType.Schema;
                    settings.Schemas.Add(schema);

                    using (XmlReader reader = XmlReader.Create(xmlStringReader, settings))
                        return (IgnoreElement)ser.Deserialize(reader);
                }
            }
            catch
            {
                return new IgnoreElement();
            }
        }

        public ICollection<string> GetIgnores(string directory)
        {
            Dictionary<string, int> results = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            GetParentIgnores(directory, results, true);
            return results.Keys;
        }

        void GetParentIgnores(string directory,
                              Dictionary<string, int> results,
                              bool includeNonRecursive)
        {
            IgnoreElement element = GetIgnoreElement(directory);

            if (Path.GetPathRoot(directory) != directory)
                GetParentIgnores(fileSystem.GetDirectoryName(directory), results, false);

            if (element.delete != null)
                foreach (string delete in element.delete)
                    results.Remove(delete);

            if (element.add != null)
                foreach (IgnoreAddElement add in element.add)
                    if (add.recursive || includeNonRecursive)
                        results[add.Value] = 1;
        }

        public bool IsIgnored(string path)
        {
            string directory = fileSystem.GetDirectoryName(path);
            string shortName = fileSystem.GetFileName(path);

            foreach (string result in GetIgnores(directory))
                if (Regex.IsMatch(shortName, PatternToRegex(result), RegexOptions.IgnoreCase))
                    return true;

            return false;
        }

        static string PatternToRegex(string pattern)
        {
            return string.Format("^{0}$", pattern.Replace(".", "\\.").Replace("*", ".*").Replace("?", "."));
        }
    }
}