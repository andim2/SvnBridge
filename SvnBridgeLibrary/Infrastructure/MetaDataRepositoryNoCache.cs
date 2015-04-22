using System;
using System.Collections.Generic;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Proxies; // TracingInterceptor
using SvnBridge.SourceControl;
using SvnBridge.Utility; // Helper.IsStringsPreciseCaseSensitivityMismatch()

namespace SvnBridge.Infrastructure
{
    [Interceptor(typeof(TracingInterceptor))]
    public class MetaDataRepositoryNoCache : MetaDataRepositoryBase
    {
        private string serverDownloadUrl;

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

            SortedList<string, SourceItem> resultUniqueSorted = new SortedList<string, SourceItem>();
            string serverDownloadUrlForParms = GetServerDownloadUrl() + "?";
            foreach (ItemSet itemSet in itemSets)
            {
                foreach (Item item in itemSet.Items)
                {
                    // This case sensitivity filtering needs to be done within this layer since this is the only layer
                    // where we have access to members itemSet.QueryPath vs. item.item required for comparison
                    // /(fortunately .QueryPath is provided, to indicate the original query,
                    // whereas .item contains the - potentially case-WRONG - source control result).
                    // FIXME: this often is not sufficient (e.g. in Recursion.Full case)
                    // since it does a compare of the single query param item only!
                    if (wantCaseSensitiveMatch)
                    {
                        if (Helper.IsStringsPreciseCaseSensitivityMismatch(itemSet.QueryPath, item.item))
                            continue; // skip mismatching result
                    }

                    if (resultUniqueSorted.ContainsKey(item.item))
                        continue; // skip existing element

                    SourceItem sourceItem = new SourceItem();
                    sourceItem.RemoteChangesetId = item.cs;
                    sourceItem.RemoteDate = item.date.ToUniversalTime();
                    sourceItem.ItemType = item.type;
                    sourceItem.ItemId = item.itemid;
                    sourceItem.RemoteName = item.item;
                    sourceItem.DownloadUrl = serverDownloadUrlForParms + item.durl;

                    resultUniqueSorted.Add(sourceItem.RemoteName, sourceItem);
                }
            }
            //   Could avoid manual Add() iteration via *ctor copy* into a List object,
            //   but better even avoid that useless temporary List object,
            //   by filling fixed-preallocation array via .CopyTo():
            //List<SourceItem> result2 = new List<SourceItem>(resultUniqueSorted.Values);
            ////foreach (SourceItem sourceItem in resultUniqueSorted.Values)
            ////{
            ////    result2.Add(sourceItem);
            ////}
            //return result2.ToArray();
            SourceItem[] result2 = new SourceItem[resultUniqueSorted.Count];
            resultUniqueSorted.Values.CopyTo(result2, 0);
            return result2;
        }

        private string GetServerDownloadUrl()
        {
            if (null == serverDownloadUrl)
            {
                // I don't know what exactly it is that is being discerned here (should add clarifications).
                // Improved check according to codeplex.com discussion #403231 "404 Errors In Log Files".
                // This is the URL as configured by Web.config's TfsUrl key.
                bool urlContainsTfsPart = (serverUrl.Contains("/tfs/") || serverUrl.EndsWith("/tfs"));
                var downloadUrlExtension = urlContainsTfsPart ? "ashx" : "asmx";
                serverDownloadUrl = serverUrl + "/VersionControl/v1.0/item." + downloadUrlExtension;
            }
            return serverDownloadUrl;
        }
    }
}
