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
        // A suitable value might be a moderately large value of 100MB -
        // reason being that nowadays this is one fifth
        // of the resources of a small system,
        // thus we should not exceed that.
        private readonly int maxEntriesLimit = MemoryUseToMaxEntriesLimit(
            100
        );

        private static int MemoryUseToMaxEntriesLimit(int sizeInMB)
        {
            // Given an assumed average size of 1kB per entry
            // we will end up at this limit value
            // for medium-severe cache resource use.
            const int entriesPerMB = 1000;
            int maxEntriesLimit = sizeInMB * entriesPerMB;
            return maxEntriesLimit;
        }

        public virtual CachedResult Get(string key)
        {
            return (CachedResult)cache[GetKeyCooked(key)];
        }

        public virtual void Set(string key, object obj)
        {
            HandleLimit(); // see useful docs at method

            cache.Add(GetKeyCooked(key),
                new CachedResult(obj), 
                null, 
                System.Web.Caching.Cache.NoAbsoluteExpiration,
                slidingExpiration,
                CacheItemPriority.Default, 
                null);
        }

        /// <remarks>
        /// While cache class has some builtin limiting mechanisms,
        /// MSDN says:
        /// "EffectivePercentagePhysicalMemoryLimit is introduced in the .NET Framework version 3.5",
        /// thus it seems older versions may have insufficient handling
        /// and thus I decided to go the version-compatible route,
        /// by implementing manual limiting of the cache object.
        ///
        /// IMPORTANT: definitely do this "potentially harmful" Limit handling
        /// only *prior* to Add()ing the new key!
        /// And in general
        /// decide to do "annoying" limit check extra handling
        /// in a *non-hotpath* (yet *reliably-invoked*!) area.
        /// E.g. it should be done
        /// in Set()-side (assumed-non-hotpath) handling
        /// rather than assumed-busier Get()...
        /// </remarks>
        private void HandleLimit()
        {
            bool isBelowEqualLimit = (maxEntriesLimit >= cache.Count);
            if (!(isBelowEqualLimit))
            {
                Clear();
            }
        }

        public virtual void Clear()
        {
            List<string> keys = new List<string>(cache.Count);
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
