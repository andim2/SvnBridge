﻿using System;
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

                    SourceItem sourceItem = new SourceItem();
                    sourceItem.RemoteChangesetId = item.cs;
                    sourceItem.RemoteDate = item.date.ToUniversalTime();
                    sourceItem.ItemType = item.type;
                    sourceItem.ItemId = item.itemid;
                    sourceItem.RemoteName = item.item;
                    var downloadUrlExtension = serverUrl.Contains("/tfs/") ? "ashx" : "asmx"; 
                    sourceItem.DownloadUrl = serverUrl + "/VersionControl/v1.0/item." + downloadUrlExtension + "?" + item.durl;

                    if (!resultUniqueSorted.ContainsKey(sourceItem.RemoteName))
                    {
                        resultUniqueSorted.Add(sourceItem.RemoteName, sourceItem);
                    }
                }
            }
            List<SourceItem> result2 = new List<SourceItem>();
            foreach (SourceItem sourceItem in resultUniqueSorted.Values)
            {
                result2.Add(sourceItem);
            }
            return result2.ToArray();
        }
    }
}
