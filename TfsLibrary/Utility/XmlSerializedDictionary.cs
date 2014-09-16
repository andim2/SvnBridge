using System;
using System.Collections;
using System.Collections.Generic;

namespace CodePlex.TfsLibrary.Utility
{
    public class XmlSerializedDictionary<TEntries, TEntry> : XmlSerializationRoot<TEntries>, IEnumerable<TEntry>
        where TEntries : new()
        where TEntry : INamedEntry
    {
        readonly Dictionary<string, TEntry> items = new Dictionary<string, TEntry>(StringComparer.InvariantCultureIgnoreCase);

        public int Count
        {
            get { return items.Keys.Count; }
        }

        public TEntry this[string name]
        {
            get
            {
                TEntry result;
                if (!items.TryGetValue(name, out result))
                    result = default(TEntry);
                return result;
            }
            set { items[name] = value; }
        }

        public IList<string> Names
        {
            get
            {
                List<string> results = new List<string>(items.Keys);
                results.Sort();
                return results;
            }
        }

        public void Add(TEntry entry)
        {
            this[entry.Name] = entry;
        }

        public void Clear()
        {
            items.Clear();
        }

        public void Delete(string name)
        {
            items.Remove(name.ToLowerInvariant());
        }

        public IEnumerator<TEntry> GetEnumerator()
        {
            foreach (string name in Names)
                yield return items[name];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}