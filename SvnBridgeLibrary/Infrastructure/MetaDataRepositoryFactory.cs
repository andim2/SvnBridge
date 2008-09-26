using System.Net;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using SvnBridge.Cache;

namespace SvnBridge.Infrastructure
{
	public class MetaDataRepositoryFactory
	{
		private readonly TFSSourceControlService sourceControlService;
        private readonly MemoryBasedPersistentCache persistentCache;
        private readonly bool cacheEnabled;

        public MetaDataRepositoryFactory(TFSSourceControlService sourceControlService, MemoryBasedPersistentCache persistentCache, bool cacheEnabled)
		{
			this.sourceControlService = sourceControlService;
			this.persistentCache = persistentCache;
            this.cacheEnabled = cacheEnabled;
		}

		public virtual IMetaDataRepository Create(ICredentials credentials, string serverUrl, string rootPath)
		{
            if (cacheEnabled)
            {
                return new MetaDataRepositoryCache(sourceControlService, credentials, persistentCache, serverUrl, rootPath);
            }
            else
            {
                return new MetaDataRepositoryNoCache(sourceControlService, credentials, serverUrl, rootPath);
            }
		}

		public virtual int GetLatestRevision(string tfsUrl, ICredentials credentials)
		{
			return sourceControlService.GetLatestChangeset(tfsUrl, credentials);
		}
	}
}