using System;
using System.Net;

namespace CodePlex.TfsLibrary.Utility
{
    public class CredentialsCache : ICredentialsCache
    {
        CredentialsCacheEntries entries;
        readonly IFileSystem fileSystem;

        public CredentialsCache(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        protected string CacheFilename
        {
            get
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string codePlexDataDirectory = fileSystem.CombinePath(homeDirectory, "Microsoft", "CodePlex Client");
                string codePlexDataFilename = fileSystem.CombinePath(codePlexDataDirectory, "CachedCredentials.xml");

                fileSystem.EnsurePath(codePlexDataDirectory);

                return codePlexDataFilename;
            }
        }

        public NetworkCredential this[string url]
        {
            get
            {
                Guard.ArgumentNotNullOrEmpty(url, "url");

                Load();
                CredentialsCacheEntry entry = entries[url];
                return (entry == null ? null : entry.ToNetworkCredential());
            }
            set
            {
                Guard.ArgumentNotNullOrEmpty(url, "url");
                Guard.ArgumentNotNull(value, "credential");

                Load();
                entries[url] = CredentialsCacheEntry.FromNetworkCredential(url, value);
                Save();
            }
        }

        public void Clear()
        {
            entries = null;

            try
            {
                fileSystem.DeleteFile(CacheFilename);
            }
            catch {}
        }

        void Load()
        {
            if (entries == null)
                entries = CredentialsCacheEntries.Deserialize(fileSystem, CacheFilename);
        }

        void Save()
        {
            if (entries != null)
                entries.Serialize(fileSystem, CacheFilename);
        }
    }
}