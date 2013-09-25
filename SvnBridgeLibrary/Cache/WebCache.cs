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
        private readonly TimeSpan slidingExpiration = TimeSpan.FromHours(2);

        public virtual CachedResult Get(string key)
        {
            return (CachedResult)cache[GetKeyCooked(key)];
        }

        public virtual void Set(string key, object obj)
        {
            cache.Add(GetKeyCooked(key),
                new CachedResult(obj), 
                null, 
                System.Web.Caching.Cache.NoAbsoluteExpiration,
                slidingExpiration,
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

        private static string GetKeyCooked(string key)
        {
            // Disabled case damaging here
            // since doing *internal* case damaging
            // of externally bona fide supplied keys
            // within a **GENERIC** web cache class
            // is very harmful
            // (for those several potential users
            // of the global WebCache
            // which do NOT want case corruption) -
            // thus definitely prefer
            // having to laboriously try re-fetching unretrievable content
            // in case of case mismatches!
            //
            // If case damaging turns out to nevertheless be required
            // for a sizeable part of users,
            // then it would likely be advisable
            // to also offer case-mangling versions
            // of Get()/Set() (~CaseMangled()?).
            // If instead only few users require that,
            // then it ought to be their own decision
            // to apply key case mangling prior to Get()/Set().

            //return key.ToLowerInvariant();
            return key;
        }
    }
}
