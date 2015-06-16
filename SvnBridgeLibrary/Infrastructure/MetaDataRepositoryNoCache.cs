using System;
using System.Collections.Generic;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Proxies; // TracingInterceptor
using SvnBridge.SourceControl;

namespace SvnBridge.Infrastructure
{
    [Interceptor(typeof(TracingInterceptor))]
    public class MetaDataRepositoryNoCache : MetaDataRepositoryBase
    {
        public MetaDataRepositoryNoCache(
            TFSSourceControlService sourceControlService,
            string serverUrl,
            ICredentials credentials,
            string rootPath)
            : base(
                sourceControlService,
                serverUrl,
                credentials,
                rootPath)
        {
        }

        public override SourceItem[] QueryItems(int revision, int itemId)
        {
            return sourceControlService.QueryItems(serverUrl, credentials,
                new int[] { itemId },
                revision,
                0);
        }

        public override SourceItem[] QueryItems(int revision, string path, Recursion recursion)
        {
            return QueryItems(revision, new string[] { path }, recursion);
        }

        public override SourceItem[] QueryItems(int revision, string[] paths, Recursion recursion)
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
            ItemSet[] itemSets = sourceControlService.QueryItems(serverUrl, credentials,
                VersionSpec.FromChangeset(revision),
                itemSpecs.ToArray(),
                0);

            SortedList<string, SourceItem> result = new SortedList<string, SourceItem>();
            foreach (ItemSet itemSet in itemSets)
            {
                foreach (Item item in itemSet.Items)
                {
                    SourceItem sourceItem = new SourceItem();
                    sourceItem.RemoteChangesetId = item.cs;
                    sourceItem.RemoteDate = item.date.ToUniversalTime();
                    sourceItem.ItemType = item.type;
                    sourceItem.ItemId = item.itemid;
                    sourceItem.RemoteName = item.item;
                    var downloadUrlExtension = serverUrl.Contains("/tfs/") ? "ashx" : "asmx"; 
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
    }
}
