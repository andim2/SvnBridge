using System;
using System.Collections.Generic;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.Proxies;
using SvnBridge.SourceControl;
using System.Configuration;
using SvnBridge.Cache;

namespace SvnBridge.Infrastructure
{
    [Interceptor(typeof(TracingInterceptor))]
    public class MetaDataRepositoryNoCache : IMetaDataRepository
    {
        private readonly TFSSourceControlService sourceControlService;
        private readonly string serverUrl;
        private readonly string rootPath;
        private readonly ICredentials credentials;

        public MetaDataRepositoryNoCache(TFSSourceControlService sourceControlService, ICredentials credentials, string serverUrl, string rootPath)
        {
            this.sourceControlService = sourceControlService;
            this.serverUrl = serverUrl;
            this.rootPath = rootPath;
            this.credentials = credentials;
        }

		public SourceItem[] QueryItems(int revision, int itemId, Recursion recursion)
		{
			return sourceControlService.QueryItems(serverUrl, credentials, new int[] { itemId }, revision);
		}

        public SourceItem[] QueryItems(int revision, string path, Recursion recursion)
        {
            return QueryItems(revision, new string[] { path }, recursion);
        }

        public SourceItem[] QueryItems(int revision, string[] paths, Recursion recursion)
        {
            List<ItemSpec> itemSpecs = new List<ItemSpec>();
            foreach (string path in paths)
            {
                ItemSpec itemspec = new ItemSpec();
                itemspec.item = GetServerPath(path);
                itemspec.recurse = RecursionType.None;
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
            ItemSet[] items = sourceControlService.QueryItems(serverUrl, credentials, VersionSpec.FromChangeset(revision), itemSpecs.ToArray());

            SortedList<string, SourceItem> result = new SortedList<string, SourceItem>();
            foreach (ItemSet itemset in items)
            {
                foreach (Item item in itemset.Items)
                {
                    SourceItem sourceItem = new SourceItem();
                    sourceItem.RemoteChangesetId = item.cs;
                    sourceItem.RemoteDate = item.date.ToUniversalTime();
                    sourceItem.ItemType = item.type;
                    sourceItem.ItemId = item.itemid;
                    sourceItem.RemoteName = item.item;
                    var downloadUrlExtension = serverUrl.Contains("/tfs") ? "ashx" : "asmx"; 
                    sourceItem.DownloadUrl = serverUrl + "/VersionControl/v1.0/item." + downloadUrlExtension + "?" + item.durl;
                    if (!result.ContainsKey(sourceItem.RemoteName))
                    {
                        result.Add(sourceItem.RemoteName, sourceItem);
                    }
                }
            }
            List<SourceItem> result2 = new List<SourceItem>();
            foreach (SourceItem sourceItem in result.Values)
            {
                result2.Add(sourceItem);
            }
            return result2.ToArray();
        }

        private string GetServerPath(string path)
        {
            if (path.StartsWith("$//"))
                return Constants.ServerRootPath + path.Substring(3);

            if (path.StartsWith("$/"))
                return path;

            string serverPath = rootPath;

            if (serverPath.EndsWith("/"))
                serverPath = serverPath.Substring(0, serverPath.Length - 1);

            if (path.StartsWith("/") == false)
                serverPath = serverPath + '/' + path;
            else
                serverPath = serverPath + path;

            if (serverPath.EndsWith("/") && serverPath != "$/")
                serverPath = serverPath.Substring(0, serverPath.Length - 1);

            return serverPath;
        }
    }
}