using System; // StringComparison
using CodePlex.TfsLibrary.ObjectModel;
using SvnBridge.SourceControl;

// XXX: using:s for MetaDataRepositoryBase only (see below)
using System.Net; // ICredentials
using System.Collections.Generic; // List
using CodePlex.TfsLibrary.RepositoryWebSvc; // ItemSpec, RecursionType
using SvnBridge.Interfaces; // IMetaDataRepository
using SvnBridge.Proxies; // TracingInterceptor

namespace SvnBridge.Interfaces
{
    public interface IMetaDataRepository
    {
        // Note: this interface has certain parameters (revision)
        // *swapped* vs. the interface (<see cref="ISourceControlService"/>)
        // that its implementations usually forward to.
        // While this may be inconvenient at implementation time
        // (manual param swapping, potential stack inefficiency),
        // I guess it was done to bring some common appearance to these methods
        // (revision is first param, at all methods).
        SourceItem[] QueryItems(int revision, int itemId);
        SourceItem[] QueryItems(
            int revision,
            int[] itemIds);
        SourceItem[] QueryItems(int revision, string path, Recursion recursion);
        SourceItem[] QueryItems(int revision, string[] paths, Recursion recursion);
    }
}

// XXX: move this class into its own new Infrastructure/MetaDataRepositoryBase.cs file!

namespace SvnBridge.Infrastructure
{
    [Interceptor(typeof(TracingInterceptor))]
    public abstract class MetaDataRepositoryBase : IMetaDataRepository
    {
        protected readonly ITFSSourceControlService sourceControlService;
        protected readonly string serverUrl;
        protected readonly ICredentials credentials;
        protected readonly string rootPath;

        protected MetaDataRepositoryBase(
            ITFSSourceControlService sourceControlService,
            string serverUrl,
            ICredentials credentials,
            string rootPath)
        {
            this.sourceControlService = sourceControlService;
            this.serverUrl = serverUrl;
            this.credentials = credentials;
            this.rootPath = rootPath;
        }

        protected string GetServerPath(string path)
        {
            if (path.StartsWith("$//"))
                return Constants.ServerRootPath + path.Substring(3);

            if (path.StartsWith("$/"))
                return path;

            string serverPath = rootPath;

            if (serverPath.EndsWith("/"))
                serverPath = serverPath.Substring(0, serverPath.Length - 1);

            serverPath += (path.StartsWith("/") == false) ?
                '/' + path
              :
                path
            ;

            if (serverPath.EndsWith("/") && !serverPath.Equals("$/"))
                serverPath = serverPath.Substring(0, serverPath.Length - 1);

            return serverPath;
        }

        //public abstract SourceItem[] QueryItems(int revision, int itemId);

        /// <summary>
        /// WARNING: at least on TFS2008, it seems ID-based SourceControlService.QueryItems()
        /// for any changeSet equal or newer of the one which deleted an item
        /// *does* still return the item
        /// (with none of its SourceItem members having a different state due to deletion!!),
        /// whereas the path-based QueryItems() *does not* return the item! (NULL result)
        /// This might be one of the many(?) victims of TFS2008 operating in "item mode" (vs. TFS2010 "slot mode").
        /// OTOH this probably just means that with TFS, items do remain in existence forever,
        /// irrespective of their "deletion state".
        /// Perhaps this behaviour of QueryItems() still returning an item even for "deleted" state
        /// is one of the reasons for having created QueryItemsExtended()...
        /// </summary>
        /// <param name="revision">Revision to query items of</param>
        /// <param name="itemId">ID of the item to be queried</param>
        /// <param name="recursion">Recursion level</param>
        /// <returns>Potentially empty array of items</returns>
        public SourceItem[] QueryItems(int revision, int itemId)
        {
            return QueryItems(
                revision,
                new int[] { itemId });
        }

        /// <summary>
        /// See important comment at our other QueryItems() implementation variant.
        /// </summary>
        /// <param name="revision">Revision to query items of</param>
        /// <param name="itemIds">Array of IDs of the items to be queried</param>
        /// <param name="recursion">Recursion level</param>
        /// <returns>Potentially empty array of items</returns>
        public SourceItem[] QueryItems(
            int revision,
            int[] itemIds)
        {
            return Service_QueryItems(
                revision,
                itemIds);
        }
        public abstract SourceItem[] QueryItems(int revision, string path, Recursion recursion);
        public abstract SourceItem[] QueryItems(int revision, string[] paths, Recursion recursion);

        protected SourceItem[] Service_QueryItems(
            int revision,
            int itemId)
        {
            return Service_QueryItems(
                revision,
                new int[] { itemId });
        }

        protected SourceItem[] Service_QueryItems(
            int revision,
            int[] itemIds)
        {
            return sourceControlService.QueryItems(serverUrl, credentials,
                itemIds,
                revision,
                0);
        }

        protected SourceItem[] Service_QueryItems(
            string path,
            RecursionType recursion,
            VersionSpec versionSpecFrom,
            DeletedState deletedState,
            ItemType itemType)
        {
            return
            sourceControlService.QueryItems(serverUrl, credentials,
                path,
                recursion,
                versionSpecFrom,
                deletedState,
                itemType,
                false, 0);
        }

        protected ItemSpec[] PathsToItemSpecs(
            string[] paths,
            Recursion recursion)
        {
            List<ItemSpec> itemSpecs = new List<ItemSpec>(paths.Length);
            foreach (string path in paths)
            {
                ItemSpec itemspec = new ItemSpec { item = GetServerPath(path), recurse = RecursionType.None };
                switch (recursion)
                {
                    case Recursion.OneLevel:
                        itemspec.recurse = RecursionType.OneLevel;
                        break;
                    case Recursion.Full:
                        itemspec.recurse = RecursionType.Full;
                        break;
                }
                itemSpecs.Add(itemspec);
            }
            return itemSpecs.ToArray();
        }
    }
}
