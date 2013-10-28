using System;
using System.Collections;
using System.Collections.Generic;
using System.Web;
using System.Web.Caching;
using SvnBridge.Interfaces;

namespace SvnBridge.Cache
{
    public class WebCache
    {
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
