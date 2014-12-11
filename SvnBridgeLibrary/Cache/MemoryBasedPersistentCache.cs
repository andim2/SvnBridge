using System;
using System.Collections.Generic;
using System.Threading;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using SvnBridge.Net;

namespace SvnBridge.Cache
{
    public delegate void Action();
    
    /// <summary>
    /// This class uses two levels of caching in order to ensure persistance.
    /// The first is the per request items, and the second is the global cache.
    /// The reason for that is that we _must_ ensure that the following code always works:
    /// <example>
    ///  cache.Set("foo", 1);
    ///  Assert.Equals(cache.Get("foo"), 1);
    /// </example>
    /// This is not valid in most caching scenarios, becaus the cache is allowed to drop the values at any time.
    /// Therefor, we use the two levls, the first level cache is per request, and is ensured to survive throughout
    /// the current request.
    /// 
    /// Reads goes first to the per request cache, and then to system cache, if it is not there.
    /// Writes goes to both of them.
    /// </summary>
    public class MemoryBasedPersistentCache
    {
        private static readonly ReaderWriterLock rwLock = new ReaderWriterLock();
        private readonly WebCache cache;

        public MemoryBasedPersistentCache(WebCache cache)
        {
            this.cache = cache;
        }

        public virtual CachedResult Get(string key)
        {
            CachedResult result = null;
            ReadLock(delegate
            {
                if (RequestCache.Items.Contains(key))
                {
                    result = new CachedResult(RequestCache.Items[key]);
                }
                else
                {
                    result = cache.Get(key);
                    if(result!=null)
                    {
                        // we have to store it back in the per request, to ensure that we
                        // wouldn't lose it during this request
                        RequestCache.Items[key] = result.Value;
                    }
                }
            });
            return result;
        }

        private static void ReadLock(Action action)
        {
            if (rwLock.IsReaderLockHeld || rwLock.IsWriterLockHeld)
            {
                action();
                return;
            }
            rwLock.AcquireReaderLock(Timeout.Infinite);
            try
            {
                action();
            }
            finally
            {
                rwLock.ReleaseReaderLock();
            }
        }

        public virtual void Set(string key, object obj)
        {
            cache.Set(key, obj);
            RequestCache.Items[key] = obj;

        }

        public virtual void UnitOfWork(Action action)
        {
            if (rwLock.IsWriterLockHeld)
            {
                action();
                return;
            }

            rwLock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                action();
            }
            finally
            {
                rwLock.ReleaseWriterLock();
            }
        }

        public virtual bool Contains(string key)
        {
            bool result = false;
            ReadLock(delegate
            {
                result = RequestCache.Items.Contains(key);
                if (result == false)
                    result = cache.Get(key) != null;
            });
            return result;
        }

        public virtual void Clear()
        {
            rwLock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                if(RequestCache.IsInitialized)
                    RequestCache.Items.Clear();
                cache.Clear();
            }
            finally
            {
                rwLock.ReleaseWriterLock();
            }
        }

        public virtual void Add(string key, string value)
        {
            if(value==null)
                return;

            HashSet<string> items;
            CachedResult result = Get(key);
            if (result != null)
            {
                items = (HashSet<string>)result.Value;
            }
            else
            {
                items = new HashSet<string>();
                Set(key, items);
            }
            items.Add(value);
        }

        public virtual List<T> GetList<T>(string key)
        {
            List<T> items = new List<T>();
            ReadLock(delegate
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
                return;
            });
            return items;
        }
    }
}
