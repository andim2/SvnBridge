using System.Collections.Generic;
using System.Threading;
using SvnBridge.Interfaces;
using SvnBridge.Net;

namespace SvnBridge.Cache
{
    public delegate void Action();
    
    /// <summary>
    /// This class uses two levels of caching in order to ensure persistence.
    /// The first is the per request items, and the second is the global cache.
    /// The reason for that is that we _must_ ensure that the following code always works:
    /// <example>
    ///  cache.Set("foo", 1);
    ///  Assert.Equals(cache.Get("foo"), 1);
    /// </example>
    /// This is not valid in most caching scenarios, becaus the cache is allowed to drop the values at any time.
    /// Therefore, we use the two levels, the first level cache is per request, and is ensured to survive throughout
    /// the current request.
    /// 
    /// Reads go first to the per request cache, and then to system cache, if it is not there.
    /// Writes go to both of them.
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
                if (RequestCacheHelper.Contains(key))
                {
                    result = new CachedResult(RequestCacheHelper.Get(key));
                }
                else
                {
                    result = cache.Get(key);
                    if(result != null)
                    {
                        // we have to store it back in the per request, to ensure that we
                        // wouldn't lose it during this request
                        RequestCacheHelper.Set(key, result.Value);
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
            RequestCacheHelper.Set(key, obj);
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
                result = RequestCacheHelper.Contains(key);
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
                if(RequestCacheHelper.IsInitialized)
                    RequestCacheHelper.Clear();
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

    /// <summary>
    /// RequestCache abstraction helper.
    /// </summary>
    internal class RequestCacheHelper
    {
        public static bool Contains(string strKey)
        {
            return RequestCache.Items.Contains(MakeSessionKey(strKey));
        }

        public static object Get(string strKey)
        {
            return RequestCache.Items[MakeSessionKey(strKey)];
        }

        public static void Set(string strKey, object objValue)
        {
            RequestCache.Items[MakeSessionKey(strKey)] = objValue;
        }

        public static bool IsInitialized
        {
            get
            {
                return RequestCache.IsInitialized;
            }
        }

        /// XXX Clears an entire somewhat globally shared cache!
        public static void Clear()
        {
            RequestCache.Items.Clear();
        }

        /// RequestCache is a somewhat globally shared cache,
        /// thus perhaps there may occur key conflicts
        /// between different sessions.
        /// FIXME actually do provide per-session mangling of the key.
        private static string MakeSessionKey(string userKey)
        {
            return userKey;
        }
    }
}
