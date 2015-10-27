using System; // TimeSpan
using System.Collections; // DictionaryEntry
using System.Collections.Generic; // List
using System.Web; // HttpRuntime.Cache
using System.Web.Caching; // CacheItemPriority
using SvnBridge.Interfaces; // CachedResult

namespace SvnBridge.Cache
{
    public class WebCache
    {
        // http://stackoverflow.com/questions/8092448/should-i-use-httpruntime-cache
        private readonly System.Web.Caching.Cache cache = HttpRuntime.Cache;

        public virtual CachedResult Get(string key)
        {
            return (CachedResult)cache[key.ToLowerInvariant()];
        }

        public virtual void Set(string key, object obj)
        {
            cache.Add(key.ToLowerInvariant(), 
                new CachedResult(obj), 
                null, 
                System.Web.Caching.Cache.NoAbsoluteExpiration,
                TimeSpan.FromHours(2), 
                CacheItemPriority.Default, 
                null);
        }

        public virtual void Clear()
        {
            List<string> keys = new List<string>();
            foreach (DictionaryEntry de in cache)
            {
                keys.Add((string)de.Key);
            }
            keys.ForEach(s => cache.Remove(s));
        }
    }
}
