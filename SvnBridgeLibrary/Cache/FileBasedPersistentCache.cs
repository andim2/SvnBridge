using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.Proxies;

namespace SvnBridge.Cache
{
    [Interceptor(typeof(RetryOnExceptionsInterceptor<IOException>))]
    public class FileBasedPersistentCache : IPersistentCache, ICanValidateMyEnvironment
    {
        private readonly string rootPath;
        private readonly ICache cache;
        private bool ensuredDirectoryExists;

        public FileBasedPersistentCache(string persistentCachePath, ICache cache)
        {
            rootPath = persistentCachePath;
            this.cache = cache;
        }

        private static int UnitOfWorkNestingLevel
        {
            get
            {
                object item = PerRequest.Items["persistent.file.cache.current.UnitOfWorkNestingLevel"];
                if (item == null)
                    return 0;
                return (int)item;
            }
            set { PerRequest.Items["persistent.file.cache.current.UnitOfWorkNestingLevel"] = value; }
        }

        private static IDictionary<string, PersistentItem> CurrentItems
        {
            get { return (IDictionary<string, PersistentItem>)PerRequest.Items["persistent.file.cache.current.items"]; }
            set { PerRequest.Items["persistent.file.cache.current.items"] = value; }
        }

        private static IDictionary<string, FileStream> CurrentFileStreams
        {
            get { return (IDictionary<string, FileStream>)PerRequest.Items["persistent.file.cache.current.files"]; }
            set { PerRequest.Items["persistent.file.cache.current.files"] = value; }
        }

        #region ICanValidateMyEnvironment Members

        public void ValidateEnvironment()
        {
            EnsureRootDirectoryExists();

            File.WriteAllText("test.write", "can write to directory");
        }

        #endregion

        #region IPersistentCache Members

        public CachedResult Get(string key)
        {
            CachedResult result = null;
            UnitOfWork(delegate
            {
                if (CurrentItems.ContainsKey(key))
                {
                    result = new CachedResult(CurrentItems[key].Item);
                    return;
                }

                if (Contains(key) == false)
                    return;

                AddToCurrentUnitOfWork(key);
                PersistentItem deserialized;
                bool hasPersistentObject = GetDeserializedObject(key, out deserialized);
                CurrentItems[key] = deserialized;
                if (hasPersistentObject == false)
                    return;
                result = new CachedResult(deserialized.Item);
            });
            return result;
        }

        public void Set(string key, object obj)
        {
            UnitOfWork(delegate
            {
                AddToCurrentUnitOfWork(key);
                CurrentItems[key] = new PersistentItem(key, obj);
            });
        }

        public bool Contains(string key)
        {
            bool contains = false;
            UnitOfWork(delegate
            {
                if (CurrentItems.ContainsKey(key))
                {
                    contains = true;
                    return;
                }
                string fileNameFromKey = GetFileNameFromKey(key);
                contains = File.Exists(fileNameFromKey);
            });
            return contains;
        }

        public void Clear()
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);
            EnsureRootDirectoryExists();
        }

        public void Add(string key, string value)
        {
            UnitOfWork(delegate
            {
                AddToCurrentUnitOfWork(key);
                CachedResult result = Get(key);
                ISet<string> set = new HashSet<string>();
                if (result != null)
                    set = (ISet<string>)result.Value;
                if (value!=null)
                    set.Add(value);
                Set(key, set);
            });
        }

        public List<T> GetList<T>(string key)
        {
            List<T> items = new List<T>();
            UnitOfWork(delegate
            {
                CachedResult result = Get(key);
                if (result == null)
                    return;

                if (result.Value is T)
                {
                    items.Add((T)result.Value);
                    return;
                }

                foreach (string itemKey in (IEnumerable<string>)result.Value)
                {
                    CachedResult itemResult = Get(itemKey);
                    if (itemResult != null)
                        items.Add((T)itemResult.Value);
                }
            });
            return items;
        }

        public void UnitOfWork(Action action)
        {
            UnitOfWorkNestingLevel += 1;
            if (UnitOfWorkNestingLevel == 1)
            {
                CurrentItems = new Dictionary<string, PersistentItem>(StringComparer.InvariantCultureIgnoreCase);
                CurrentFileStreams = new Dictionary<string, FileStream>(StringComparer.InvariantCultureIgnoreCase);
            }
            bool hasException = false;
            try
            {
                action();
                if (UnitOfWorkNestingLevel == 1)
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    foreach (PersistentItem item in CurrentItems.Values)
                    {
                        if (item.Changed == false)
                            continue;
                        FileStream stream = CurrentFileStreams[item.Name];
                        stream.Position = 0;
                        bf.Serialize(stream, item);
                    }
                }
            }
            catch
            {
                hasException = true;
                throw;
            }
            finally
            {
                if (hasException == false && UnitOfWorkNestingLevel == 1)
                {
                    foreach (FileStream value in CurrentFileStreams.Values)
                    {
                        value.Flush();
                        value.Dispose();
                    }
                    CurrentFileStreams = null;
                    CurrentItems = null;
                }
                UnitOfWorkNestingLevel -= 1;
            }
        }

        #endregion

        private static FileStream GetCurrentStream(string key)
        {
            FileStream value;
            if (CurrentFileStreams.TryGetValue(key, out value))
                return value;
            return null;
        }

        private static bool GetDeserializedObject(string key, out PersistentItem deserialized)
        {
            deserialized = null;
            if (GetCurrentStream(key).Length == 0) //empty file
                return false;

            BinaryFormatter formatter = new BinaryFormatter();
            deserialized = (PersistentItem)formatter.Deserialize(GetCurrentStream(key));

            // we need to do this because we may get collisions
            // in the keys, the chances are not good for this, because we use 
            // cryptographically significant hashing for this, but we need to take this
            // into account
            // Note: we need to make a case insensitive comparison, because we get
            // different casing from the client

            return string.Equals(deserialized.Name, key, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// This locks the file, giving us a very crude form of locking
        /// </summary>
        /// <param name="key"></param>
        private void AddToCurrentUnitOfWork(string key)
        {
            if (GetCurrentStream(key) != null) //already there
                return;

            string fileNameFromKey = GetFileNameFromKey(key);
            EnsureDirectoryExists(Path.GetDirectoryName(fileNameFromKey));
            CurrentFileStreams[key] =
                File.Open(fileNameFromKey, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private string GetFileNameFromKey(string key)
        {
            // Note: we need to make a case insensitive comparison
            string filename = key.ToLowerInvariant();
            return Path.Combine(rootPath, FileNameHashing.HashIfNeeded(rootPath, filename));
        }

        private void EnsureRootDirectoryExists()
        {
            if (ensuredDirectoryExists)
                return;
            ensuredDirectoryExists = true;
            EnsureDirectoryExists(rootPath);
        }

        private void EnsureDirectoryExists(string directoryName)
        {
            string cacheKey = "EnsureDirectoryExists: " + directoryName;
            if (cache.Get(cacheKey) != null)
                return;
            if (Directory.Exists(directoryName) == false)
            {
                Directory.CreateDirectory(directoryName);
            }
            cache.Set(cacheKey, true);
        }

        #region Nested type: PersistentItem

        [Serializable]
        public class PersistentItem
        {
            [NonSerialized]
            public bool Changed;
            public object Item;
            public string Name;

            public PersistentItem()
            {
            }


            public PersistentItem(string name, object item)
            {
                Name = name;
                Item = item;
                Changed = true;
            }
        }

        #endregion
    }
}
