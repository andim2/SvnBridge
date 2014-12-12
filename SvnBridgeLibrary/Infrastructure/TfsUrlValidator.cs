using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.Utility;
using SvnBridge.Cache;

namespace SvnBridge.Infrastructure
{
    public class TfsUrlValidator
    {
		private WebCache cache;

		public TfsUrlValidator(WebCache cache)
		{
			this.cache = cache;
		}

		public virtual bool IsValidTfsServerUrl(string url)
		{
			string cacheKey = "IsValidTfsServerUrl_" + url;
			CachedResult result = cache.Get(cacheKey);
			if (result != null)
				return (bool) result.Value;
			bool validUrl = Helper.IsValidTFSUrl(url, Proxy.DefaultProxy);
			cache.Set(cacheKey, validUrl);
			return validUrl;
		}
	}
}
