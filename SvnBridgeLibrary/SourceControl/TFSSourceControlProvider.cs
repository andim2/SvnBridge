using System.Net.Sockets; // SocketException
using CodePlex.TfsLibrary;
using SvnBridge.Net; // RequestCache

namespace SvnBridge.SourceControl
{
    using System;
    using System.Collections.Generic;
    using System.IO; // FileNotFoundException only
    using System.Net; // ICredentials
    using System.Text.RegularExpressions; // Regex
    using CodePlex.TfsLibrary.ObjectModel;
    using CodePlex.TfsLibrary.RepositoryWebSvc;
    using Dto;
    using Exceptions; // FolderAlreadyExistsException
    using Infrastructure;
    using Interfaces; // IMetaDataRepository
    using Protocol; // UpdateReportData only (layer violation?)
    using Proxies; // TracingInterceptor, RetryOnExceptionsInterceptor
    using Utility;
    using SvnBridge.Cache;
    using System.Web.Services.Protocols; // SoapException
    using System.Linq; // System.Array extensions

    /// <summary>
    /// I don't quite know yet where to place these things,
    /// but I do know that they shouldn't be needlessly restricted to
    /// use within TFSSourceControlProvider only...
    /// These file system path calculation helpers
    /// are purely about path handling
    /// i.e.: NOT ItemMetaData-based.
    /// </summary>
    public sealed class FilesysHelpers
    {
        public static void StripRootSlash(ref string path)
        {
            if (path.StartsWith("/"))
                path = path.Substring(1);
        }

        /// <summary>
        /// Helper to abstract/hide away the *internal* decision
        /// on whether names of filesystem items ought to be case-mangled
        /// (to repair TFS-side case-insensitive / case sensitivity handling issues).
        /// It seems we have issues with caching file information wrongly
        /// due to ToLower()ing filenames when in fact there are cases of similar-name
        /// (changed case) renaming for certain changesets.
        /// Thus we decide to NOT do ToLower() in case of case-sensitive operation mode...
        /// </summary>
        /// <param name="nameOrig">Original (likely not-yet-mangled) name</param>
        public static string GetCaseMangledName(string nameOrig)
        {
            // I don't think it's useful to have this bool
            // be made a local class member -
            // after all this functionality
            // should always directly follow the current Configuration-side setting.
            bool wantCaseSensitiveMatch = Configuration.SCMWantCaseSensitiveItemMatch; // CS0429 warning workaround
            return wantCaseSensitiveMatch ? nameOrig : nameOrig.ToLower();
        }
    }

    [Interceptor(typeof(TracingInterceptor))]
    [Interceptor(typeof(RetryOnExceptionsInterceptor<SocketException>))]
    public class TFSSourceControlProvider : MarshalByRefObject
    {
        private static readonly Regex s_associatedWorkItems = new Regex(@"(?:(?:(?:fixe?|close|resolve)(?:s|d)?)|(?:Work ?Items?))(?: |:|: )(#?\d+(?:, ?#?\d+)*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);
        private const char c_workItemChar = '#';

        private readonly string rootPath;
        private readonly string serverUrl;
        private readonly int maxLengthFromRootPath;
        private readonly ICredentials credentials;
        private readonly TFSSourceControlService sourceControlService;
        private readonly IWorkItemModifier workItemModifier;
        private readonly DefaultLogger logger;
        private readonly WebCache cache;
        private readonly IMetaDataRepository metaDataRepository;
        private readonly FileRepository fileRepository;

        public TFSSourceControlProvider(
            string serverUrl,
            string projectName,
            ICredentials credentials,
            TFSSourceControlService sourceControlService,
            IWorkItemModifier workItemModifier,
            DefaultLogger logger,
            WebCache cache,
            FileRepository fileRepository)
        {
            this.serverUrl = serverUrl;
            this.credentials = CredentialsHelper.GetCredentialsForServer(this.serverUrl, credentials);
            this.sourceControlService = sourceControlService;
            this.workItemModifier = workItemModifier;
            this.logger = logger;
            this.cache = cache;
            this.fileRepository = fileRepository;

            rootPath = Constants.ServerRootPath;
            if (!string.IsNullOrEmpty(projectName))
            {
                rootPath += projectName + "/";
            }
            // Hmm, what is the actual reason for the magic 259 value??
            // Probably it's due to Win32 MAX_PATH (260) "minus 1 something" (most likely trailing \0).
            // Since there's no MAX_PATH constant in C#, we'll just keep it open-coded.
            // If the MAX_PATH limitation turns out to be too painful, then perhaps the UNC path convention
            // ("\\?\" prefix, 32k chars limit) might actually be usable here.
            this.maxLengthFromRootPath = 259 - rootPath.Length;
            if (Configuration.CacheEnabled)
            {
                this.metaDataRepository = new MetaDataRepositoryCache(
                    this.sourceControlService,
                    this.serverUrl,
                    this.credentials,
                    this.rootPath,
                    Container.Resolve<MemoryBasedPersistentCache>());
            }
            else
            {
                this.metaDataRepository = new MetaDataRepositoryNoCache(
                    this.sourceControlService,
                    this.serverUrl,
                    this.credentials,
                    this.rootPath);
            }
        }

        public virtual void CopyItem(string activityId, string path, string targetPath)
        {
            CopyAction copyAction = new CopyAction(path, targetPath, false);
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                activity.CopiedItems.Add(copyAction);
            });
            ProcessCopyItem(activityId, copyAction, false);
        }

        public virtual void DeleteActivity(string activityId)
        {
            sourceControlService.DeleteWorkspace(serverUrl, credentials, activityId);
            ActivityRepository.Delete(activityId);
        }

        public virtual bool DeleteItem(string activityId, string path)
        {
            if ((GetItems(-1, path, Recursion.None, true) == null) && (GetPendingItem(activityId, path) == null))
            {
                return false;
            }

            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                bool postCommitDelete = false;
                foreach (CopyAction copy in activity.CopiedItems)
                {
                    if (copy.Path.StartsWith(path + "/"))
                    {
                        if (!activity.PostCommitDeletedItems.Contains(path))
                        {
                            activity.PostCommitDeletedItems.Add(path);
                        }

                        if (!copy.Rename)
                        {
                            ConvertCopyToRename(activityId, copy);
                        }

                        postCommitDelete = true;
                    }
                }

                if (!postCommitDelete)
                {
                    bool deleteIsRename = false;
                    foreach (CopyAction copy in activity.CopiedItems)
                    {
                        if (copy.Path == path)
                        {
                            ConvertCopyToRename(activityId, copy);
                            deleteIsRename = true;
                        }
                    }
                    if (!deleteIsRename)
                    {
                        ProcessDeleteItem(activityId, path);
                        activity.DeletedItems.Add(path);
                    }
                }
            });
            return true;
        }

        public virtual FolderMetaData GetChangedItems(
            string path,
            int versionFrom,
            int versionTo,
            UpdateReportData reportData)
        {
            SVNPathStripLeadingSlash(ref path);

            var root = (FolderMetaData)GetItems(versionTo, path, Recursion.None);

            if (root != null)
            {
                root.Properties.Clear();
            }

            // the item doesn't exist and the request was for a specific target
            if (root == null && reportData.UpdateTarget != null)
            {
                root = new FolderMetaData();
                var deletedFile = new DeleteMetaData
                {
                    ItemRevision = versionTo,
                    Name = reportData.UpdateTarget
                };
                root.Items.Add(deletedFile);
                return root;
            }
            if (root == null)
            {
                throw new FileNotFoundException(path);
            }

            var udc = new UpdateDiffCalculator(this);
            udc.CalculateDiff(path, versionTo, versionFrom, root, reportData);
            if (reportData.UpdateTarget != null)
            {
                string targetPath = "/" + Helper.CombinePath(path, reportData.UpdateTarget);
                // [cannot easily use List.RemoveAll() here]
                foreach (ItemMetaData item in new List<ItemMetaData>(root.Items))
                {
                    if (!item.IsSamePath(targetPath))
                        root.Items.Remove(item);
                }
            }
            return root;
        }


        public virtual ItemMetaData GetItemInActivity(string activityId, string path)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                foreach (CopyAction copy in activity.CopiedItems)
                {
                    if (path.StartsWith(copy.TargetPath))
                    {
                        path = copy.Path + path.Substring(copy.TargetPath.Length);
                    }
                }
            });
            return GetItemsWithoutProperties(-1, path, Recursion.None);
        }

        public virtual ItemMetaData GetItems(int version, string path, Recursion recursion)
        {
            return GetItems(version, path, recursion, false);
        }

        public virtual ItemMetaData GetItemsWithoutProperties(int version, string path, Recursion recursion)
        {
            return GetItems(version, path, recursion, false);
        }

        /// <summary>
        /// We are caching the value, to avoid expensive remote calls. 
        /// This is safe to do because <see cref="TFSSourceControlProvider"/> is a transient
        /// type, and will only live for the current request.
        /// </summary>
        /// <returns></returns>
        public virtual int GetLatestVersion()
        {
            const string latestVersion = "Repository.Latest.Version";
            if (RequestCache.Items[latestVersion] == null)
            {
                RequestCache.Items[latestVersion] = sourceControlService.GetLatestChangeset(serverUrl, credentials);
            }
            return (int)RequestCache.Items[latestVersion];
        }

        public virtual LogItem GetLog(
            string path,
            int versionFrom,
            int versionTo,
            Recursion recursion,
            int maxCount,
            bool sortAscending = false)
        {
            return GetLog(
                path,
                -1,
                versionFrom,
                versionTo,
                recursion,
                maxCount,
                sortAscending);
        }

        public virtual LogItem GetLog(
            string path,
            int itemVersion,
            int versionFrom,
            int versionTo,
            Recursion recursion,
            int maxCount,
            bool sortAscending = false)
        {
            SVNPathStripLeadingSlash(ref path);

            string serverPath = MakeTfsPath(path);
            // SVNBRIDGE_WARNING_REF_RECURSION
            RecursionType recursionType = RecursionType.None;
            switch (recursion)
            {
                case Recursion.OneLevel:
                    // Hmm, why is this translated to .None here?
                    // There was neither a comment here nor was it encapsulated into a self-explanatory
                    // helper method.
                    // Perhaps it's for correcting OneLevel requests
                    // which probably don't make sense with log-type SVN queries... right?
                    recursionType = RecursionType.None;
                    break;
                case Recursion.Full:
                    recursionType = RecursionType.Full;
                    break;
            }

            VersionSpec itemVersionSpec = VersionSpec.Latest;
            if (itemVersion != -1)
                itemVersionSpec = VersionSpec.FromChangeset(itemVersion);

            // WARNING: TFS08 QueryHistory() is very problematic! (see comments in next inner layer)
            SourceItemHistory[] histories = QueryHistory(
                serverPath,
                itemVersionSpec,
                versionFrom,
                versionTo,
                recursionType,
                maxCount,
                sortAscending).ToArray();

            foreach (SourceItemHistory history in histories)
            {
                List<SourceItem> renamedItems = new List<SourceItem>();
                List<SourceItem> branchedItems = new List<SourceItem>();

                foreach (SourceItemChange change in history.Changes)
                {
                    if (change.Item.RemoteName.Length > rootPath.Length)
                        change.Item.RemoteName = change.Item.RemoteName.Substring(rootPath.Length);
                    else
                        change.Item.RemoteName = "";

                    if ((change.ChangeType & ChangeType.Rename) == ChangeType.Rename)
                    {
                        renamedItems.Add(change.Item);
                    }
                    else if ((change.ChangeType & ChangeType.Branch) == ChangeType.Branch)
                    {
                        branchedItems.Add(change.Item);
                    }
                }
                if (renamedItems.Count > 0)
                {
                    // I had pondered "improving" naming of variables old* to preRename*,
                    // however that might be imprecise
                    // since it's possibly not only in a _rename_ change
                    // that there are "old items",
                    // but also with _copy_ (branch) or even other changes.
                    ItemMetaData[] oldItems = GetPreviousVersionOfItems(renamedItems.ToArray(), history.ChangeSetID);
                    var oldItemsById = new Dictionary<int, ItemMetaData>();
                    // I pondered changing this loop into the (faster) decrementing type,
                    // but I'm unsure: I wonder whether
                    // having rename actions/items processed in strict incrementing order
                    // is actually *required* (since they might be inter-dependent).
                    for (var i = 0; i < renamedItems.Count; i++)
                    {
                        if (oldItems[i] != null)
                            oldItemsById[renamedItems[i].ItemId] = oldItems[i];
                    }

                    var renamesWithNoPreviousVersion = new List<SourceItemChange>();
                    foreach (var change in history.Changes.Where(change => (change.ChangeType & ChangeType.Rename) == ChangeType.Rename))
                    {
                        ItemMetaData oldItem;
                        if (oldItemsById.TryGetValue(change.Item.ItemId, out oldItem))
                            change.Item = new RenamedSourceItem(change.Item, oldItem.Name, oldItem.Revision);
                        else
                            renamesWithNoPreviousVersion.Add(change);
                    }

                    // [this is slowpath (rare event),
                    // thus Remove() is better than Enumerable.Except() use:]
                    foreach (var rename in renamesWithNoPreviousVersion)
                        history.Changes.Remove(rename);

                    history.Changes.RemoveAll(item => item.ChangeType == ChangeType.None);
                    history.Changes.RemoveAll(item => item.ChangeType == ChangeType.Delete &&
                                              oldItems.Any(oldItem => oldItem != null && oldItem.Id == item.Item.ItemId));
                }
                if (branchedItems.Count > 0)
                {
                    var itemsBranched = branchedItems.Select(item => CreateItemSpec(MakeTfsPath(item.RemoteName), RecursionType.None)).ToArray();

                    ChangesetVersionSpec branchChangeset = new ChangesetVersionSpec();
                    branchChangeset.cs = history.ChangeSetID;
                    BranchRelative[][] branches = sourceControlService.QueryBranches(serverUrl, credentials, null, itemsBranched, branchChangeset);

                    foreach (BranchRelative[] branch in branches)
                    {
                        foreach (SourceItem item in branchedItems)
                        {
                            foreach (BranchRelative branchItem in branch)
                            {
                                if (item.ItemId == branchItem.BranchToItem.itemid)
                                {
                                    foreach (SourceItemChange change in history.Changes)
                                    {
                                        if (change.Item.ItemId == item.ItemId)
                                        {
                                            string oldName = branchItem.BranchFromItem.item.Substring(rootPath.Length);
                                            int oldRevision = item.RemoteChangesetId - 1;
                                            change.Item = new RenamedSourceItem(item, oldName, oldRevision);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            LogItem logItem = new LogItem(null, serverPath, histories);

            return logItem;
        }

        private List<SourceItemHistory> ConvertChangesetsToSourceItemHistory(Changeset[] changesets)
        {
            List<SourceItemHistory> history = new List<SourceItemHistory>();

            foreach (Changeset changeset in changesets)
            {
                SourceItemHistory sourceItemHistory = ConstructSourceItemHistoryFromChangeset(
                    changeset);
                foreach (Change change in changeset.Changes)
                {
                    if (!IsPropertyFolder(change.Item.item))
                    {
                        SourceItem sourceItem;
                        if (!IsPropertyFile(change.Item.item))
                        {
                            sourceItem = SourceItem.FromRemoteItem(change.Item.itemid, change.Item.type, change.Item.item, change.Item.cs, change.Item.len, change.Item.date, null);
                            if ((change.type == (ChangeType.Add | ChangeType.Edit | ChangeType.Encoding)) ||
                               (change.type == (ChangeType.Add | ChangeType.Encoding)))
                                sourceItemHistory.Changes.Add(new SourceItemChange(sourceItem, ChangeType.Add));
                            else
                                sourceItemHistory.Changes.Add(new SourceItemChange(sourceItem, change.type));
                        }
                        else
                        {
                            string item = GetItemFileNameFromPropertiesFileName(change.Item.item);
                            bool itemFileIncludedInChanges = false;
                            foreach (Change itemChange in changeset.Changes)
                            {
                                if (itemChange.Item.item == item)
                                {
                                    itemFileIncludedInChanges = true;
                                }
                            }
                            if (!itemFileIncludedInChanges)
                            {
                                if (change.Item.item.EndsWith(Constants.PropFolder + "/" + Constants.FolderPropFile))
                                    sourceItem = SourceItem.FromRemoteItem(change.Item.itemid, ItemType.Folder, item, change.Item.cs, change.Item.len, change.Item.date, null);
                                else
                                    sourceItem = SourceItem.FromRemoteItem(change.Item.itemid, ItemType.File, item, change.Item.cs, change.Item.len, change.Item.date, null);

                                sourceItemHistory.Changes.Add(new SourceItemChange(sourceItem, ChangeType.Edit));
                            }
                        }
                    }
                }
                history.Add(sourceItemHistory);
            }

            return history;
        }

        private static SourceItemHistory ConstructSourceItemHistoryFromChangeset(
            Changeset changeset)
        {
            // Username used to get set to changeset.cmtr, but at
            // least for VSS-migrated repositories and for gated
            // checkins this is wrong, thus try using changeset.owner.
            // For details and possible variants to get this fixed,
            // please see "Log Messages Issue - Committer vs. Owner when
            // using Gated Check-In"
            //   http://svnbridge.codeplex.com/discussions/260147
            return new SourceItemHistory(
                changeset.Changes[0].Item.cs,
                changeset.owner,
                changeset.date,
                changeset.Comment);
        }

        /// WARNING: the service-side QueryHistory() API will silently discard **older** entries
        /// in case maxCount is not big enough to hold all history entries!
        /// When attempting to linearly iterate from a very old revision to a much newer one (i.e., huge range)
        /// this is a *PROBLEM* since it's not easy to pick up where the result left us
        /// (i.e. within your older->newer loop you definitely end up
        /// with an unwanted newer-entries result part first,
        /// despite going from old to new).
        /// Certain (newer, it seems) variants of MSDN VersionControlServer.QueryHistory()
        /// include a sortAscending param which might be helpful to resolve this,
        /// but I don't know whether it can be used.
        /// Thus currently the only way to ensure non-clipped history
        /// is to supply maxCount int.MaxValue.
        ///
        /// Since call hierarchy of history handling is a multitude of private calls,
        /// it might be useful to move all this class-bloating handling
        /// into a separate properly isolated class specifically for this purpose.
        private List<SourceItemHistory> QueryHistory(
            string serverPath,
            VersionSpec itemVersion,
            int versionFrom,
            int versionTo,
            RecursionType recursionType,
            int maxCount,
            bool sortAscending)
        {
            List<SourceItemHistory> histories;

            ItemSpec itemSpec = CreateItemSpec(serverPath, recursionType);
            VersionSpec versionSpecFrom = VersionSpec.FromChangeset(versionFrom);
            VersionSpec versionSpecTo = VersionSpec.FromChangeset(versionTo);
            // Since we'll potentially have multi-query,
            // maintain a helper to track how many additional items we're allowed to add:
            int maxCount_Allowed = maxCount;
            Changeset[] changesets;
            try
            {
                changesets = Service_QueryHistory(
                    itemSpec, itemVersion,
                    versionSpecFrom, versionSpecTo,
                    maxCount_Allowed,
                    sortAscending);
            }
            catch (SoapException ex)
            {
                if ((recursionType == RecursionType.Full) && (ex.Message.EndsWith(" does not exist at the specified version.")))
                {
                    // Workaround for bug in TFS2008sp1
                    int latestVersion = GetLatestVersion();
                    // WARNING: TFS08 QueryHistory() is very problematic! (see comments here and in next inner layer)
                    List<SourceItemHistory> tempHistories = QueryHistory(
                        serverPath,
                        itemVersion,
                        1,
                        latestVersion,
                        RecursionType.None,
                        2,
                        sortAscending /* is this the value to pass to have this workaround still work properly? */);
                    if (tempHistories[0].Changes[0].ChangeType == ChangeType.Delete && tempHistories.Count == 2)
                        latestVersion = tempHistories[1].ChangeSetID;

                    if (versionTo == latestVersion)
                    {
                        // in this case, there are only 2 revisions in TFS
                        // the first being the initial checkin, and the second
                        // being the deletion, there is no need to query further
                        histories = tempHistories;
                    }
                    else
                    {
                        string itemFirstPath = tempHistories[0].Changes[0].Item.RemoteName; // debug helper
                        histories = QueryHistory(
                            itemFirstPath,
                            VersionSpec.FromChangeset(latestVersion),
                            1,
                            latestVersion,
                            RecursionType.Full,
                            int.MaxValue,
                            sortAscending);
                    }

                    // I don't know whether we actually want/need to do ugly manual version limiting here -
                    // perhaps it would be possible to simply restrict the queries above up to versionTo,
                    // but perhaps these queries were being done this way since perhaps e.g. for merge operations
                    // (nonlinear history) version ranges of a query do need to be specified in full.
                    Histories_RestrictToRangeWindow(
                        ref histories,
                        versionTo,
                        maxCount,
                        false);

                    return histories;
                }
                else
                    throw;
            }
            List<Changeset> changesetsTotal = new List<Changeset>();

            changesetsTotal.AddRange(changesets);

            int logItemsCount_ThisRun = changesets.Length;

            // TFS QueryHistory API won't return more than 256 items,
            // so need to call multiple times if more requested
            // IMPLEMENTATION WARNING: since the 256 items limit
            // clearly is a *TFS-side* limitation,
            // make sure to always keep this correction handling code
            // situated within inner TFS-side handling layers!!
            const int TFS_QUERY_LIMIT = 256;
            bool didHitPossiblyPrematureLimit = ((logItemsCount_ThisRun == TFS_QUERY_LIMIT) && (maxCount_Allowed > TFS_QUERY_LIMIT));
            if (didHitPossiblyPrematureLimit)
            {
                for (; ; )
                {
                    didHitPossiblyPrematureLimit = (TFS_QUERY_LIMIT == logItemsCount_ThisRun);
                    bool needContinueQuery = (didHitPossiblyPrematureLimit);
                    if (!(needContinueQuery))
                    {
                        break;
                    }
                    // Confirmed! We *did* get TFS_QUERY_LIMIT entries this time,
                    // yet request *was* larger than that,
                    // so there might be further entries remaining...

                    int earliestVersionFound = changesets[changesets.Length - 1].cset - 1;
                    if (earliestVersionFound == versionFrom)
                        break;

                    maxCount_Allowed -= logItemsCount_ThisRun;

                    versionSpecTo = VersionSpec.FromChangeset(earliestVersionFound);

                    changesets = Service_QueryHistory(
                        itemSpec, itemVersion,
                        versionSpecFrom, versionSpecTo,
                        maxCount_Allowed,
                        sortAscending);
                    changesetsTotal.AddRange(changesets);
                    logItemsCount_ThisRun = changesets.Length;
                }
            }

            histories = ConvertChangesetsToSourceItemHistory(changesetsTotal.ToArray());

            return histories;
        }

        private Changeset[] Service_QueryHistory(
            ItemSpec itemSpec, VersionSpec itemVersion,
            VersionSpec versionSpecFrom, VersionSpec versionSpecTo,
            int maxCount,
            bool sortAscending)
        {
            Changeset[] changesets;

            // WARNING!! QueryHistory() (at least on TFS08) is very problematic, to say the least!
            // E.g. for a folder renamed-away into a subdir,
            // doing a query on its *previous* location, with an itemVersion/versionFrom/versionTo config that's properly pointing
            // at the prior state, will fail to yield any history. Only by doing a query on the still-existing *parent* folder
            // with these revision ranges will one manage to retrieve the proper history records of the prior folder location.
            // A somewhat related (but then not really...) SVN attribute is strict-node-history.
            changesets = sourceControlService.QueryHistory(serverUrl, credentials,
                null, null,
                itemSpec, itemVersion,
                null,
                versionSpecFrom, versionSpecTo,
                maxCount,
                true, false, false,
                sortAscending);

            return changesets;
        }

        /// <summary>
        /// Restrict a possibly overly wide list of changesets to a certain desired range,
        /// by passing a maximum version to be listed,
        /// and by subsequently restricting the number of entries to maxCount.
        /// </summary>
        /// <param name="histories">List of changesets to be modified</param>
        /// <param name="versionTo">maximum version to keep listing</param>
        /// <param name="maxCount">maximum number of entries allowed</param>
        /// <param name="whenOverflowDiscardNewest">when true: remove newest version entries, otherwise remove oldest.
        /// Hmm... not sure whether offering a whenOverflowDiscardNewest choice is even helpful -
        /// perhaps the user should always expect discarding at a certain end and thus _always_
        /// have loop handling for missing parts...
        /// </param>
        private static void Histories_RestrictToRangeWindow(
            ref List<SourceItemHistory> histories,
            int versionTo,
            int maxCount,
            bool whenOverflowDiscardNewest)
        {
            while ((histories.Count > 0) && (histories[0].ChangeSetID > versionTo))
            {
                histories.RemoveAt(0);
            }
            var numElemsExceeding = histories.Count - maxCount;
            bool isCountWithinRequestedLimit = (0 >= numElemsExceeding);
            if (!(isCountWithinRequestedLimit))
            {
                // Order of the results that TFS returns is from _newest_ (index 0) to oldest (last index),
                // thus when whenOverflowDiscardNewest == true we need to remove the starting range,
                // else end range.
                var numElemsRemove = numElemsExceeding;
                int startIndex = whenOverflowDiscardNewest ? 0 : maxCount;
                histories.RemoveRange(startIndex, numElemsRemove);
            }
        }

        public virtual bool IsDirectory(int version, string path)
        {
            ItemMetaData item = GetItemsWithoutProperties(version, path, Recursion.None);
            return item.ItemType == ItemType.Folder;
        }

        public virtual bool ItemExists(string path)
        {
            return ItemExists(path, -1);
        }

        public virtual bool ItemExists(string path, int version)
        {
            bool itemExists = false;
            ItemMetaData item = GetItems(version, path, Recursion.None, true);
            if (item != null)
            {
                itemExists = true;
                bool needCheckCaseSensitiveItemMatch = (Configuration.SCMWantCaseSensitiveItemMatch);
                if (needCheckCaseSensitiveItemMatch)
                {
                    SVNPathStripLeadingSlash(ref path);
                    itemExists = false;
                    bool haveCorrectlyCasedItem = item.Name.Equals(path);
                    if (haveCorrectlyCasedItem)
                        itemExists = true;
                }
            }
            return itemExists;
        }

        public virtual bool ItemExists(int itemId, int version)
        {
            if (0 == itemId)
                throw new ArgumentException("item id cannot be zero", "itemId");
            var items = metaDataRepository.QueryItems(version, itemId);
            return (items.Length != 0);
        }

        public virtual void MakeActivity(string activityId)
        {
            ClearExistingTempWorkspaces(true);

            sourceControlService.CreateWorkspace(serverUrl, credentials, activityId, Constants.WorkspaceComment);
            string localPath = GetLocalPath(activityId, "");
            sourceControlService.AddWorkspaceMapping(serverUrl, credentials, activityId, rootPath, localPath, 0);
            ActivityRepository.Create(activityId);
        }

        private void ClearExistingTempWorkspaces(bool skipExistingActivities)
        {
            WorkspaceInfo[] workspaces = sourceControlService.GetWorkspaces(serverUrl, credentials, WorkspaceComputers.ThisComputer, 0);
            foreach (WorkspaceInfo workspace in workspaces)
            {
                if (workspace.Comment != Constants.WorkspaceComment)
                    continue;
                if (skipExistingActivities && ActivityRepository.Exists(workspace.Name))
                    continue;
                sourceControlService.DeleteWorkspace(serverUrl, credentials,
                                                     workspace.Name);
                ActivityRepository.Delete(workspace.Name);
            }
        }

        public virtual void MakeCollection(string activityId, string path)
        {
            if (ItemExists(path))
            {
                // [Folder pre-existing in repository??
                // Possibly your repo working copy was not up-to-date
                // - try updating and *then* retry commit...]
                throw new FolderAlreadyExistsException();
            }

            ItemMetaData item;
            string existingPath = path.Substring(1);
            do
            {
                if (existingPath.IndexOf('/') != -1)
                {
                    existingPath = existingPath.Substring(0, existingPath.LastIndexOf('/'));
                }
                else
                {
                    existingPath = "";
                }

                item = GetItemsWithoutProperties(-1, existingPath, Recursion.None);
            } while (item == null);
            string localPath = GetLocalPath(activityId, path);
            UpdateLocalVersion(activityId, item, localPath.Substring(0, localPath.LastIndexOf('\\')));

            ServiceSubmitPendingRequest(activityId, PendRequest.AddFolder(localPath));
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                activity.MergeList.Add(
                    new ActivityItem(MakeTfsPath(path), ItemType.Folder, ActivityItemAction.New));
                activity.Collections.Add(path);
            });

        }

        /// <summary>
        /// The main public interface handler for WebDAV MERGE request.
        /// Commits the recorded transaction contents on the server.
        /// </summary>
        /// <param name="activityId">ID of the activity (transaction)</param>
        /// <returns>MergeActivityResponse object</returns>
        public virtual MergeActivityResponse MergeActivity(string activityId)
        {
            MergeActivityResponse response = null;
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                UpdateProperties(activityId);
                List<string> commitServerList = new List<string>();
                foreach (ActivityItem item in activity.MergeList)
                {
                    if (item.Action != ActivityItemAction.RenameDelete)
                    {
                        commitServerList.Add(item.Path);
                    }
                    if (item.Action == ActivityItemAction.Branch)
                    {
                        SourceItem[] items = metaDataRepository.QueryItems(GetLatestVersion(), item.SourcePath, Recursion.Full);
                        foreach (SourceItem sourceItem in items)
                        {
                            string branchedPath = item.Path + sourceItem.RemoteName.Substring(item.SourcePath.Length);
                            if (commitServerList.Contains(branchedPath) == false)
                                commitServerList.Add(branchedPath);
                        }
                    }
                }

                int changesetId;
                if (commitServerList.Count > 0)
                {
                    try
                    {
                        changesetId =
                            sourceControlService.Commit(serverUrl, credentials,
                                activityId,
                                activity.Comment,
                                commitServerList,
                                false, 0);
                    }
                    catch (TfsFailureException)
                    {
                        // we just failed a commit, this tends to happen when we have a conflict
                        // between previously partially committed changes and the current changes.
                        // We will wipe all the user's temporary workspaces and allow the user to 
                        // try again
                        ClearExistingTempWorkspaces(false);

                        throw;
                    }
                }
                else
                {
                    changesetId = GetLatestVersion();
                }

                if (activity.PostCommitDeletedItems.Count > 0)
                {
                    commitServerList.Clear();
                    foreach (string path in activity.PostCommitDeletedItems)
                    {
                        ProcessDeleteItem(activityId, path);
                        commitServerList.Add(MakeTfsPath(path));
                    }
                    changesetId =
                        sourceControlService.Commit(serverUrl, credentials,
                            activityId,
                            activity.Comment,
                            commitServerList,
                            false, 0);
                }
                AssociateWorkItemsWithChangeSet(activity.Comment, changesetId);
                response = GenerateMergeResponse(activityId, changesetId);
            });

            return response;
        }

        public virtual void AssociateWorkItemsWithChangeSet(string comment, int changesetId)
        {
            MatchCollection matches = s_associatedWorkItems.Matches(comment ?? string.Empty);
            foreach (Match match in matches)
            {
                Group group = match.Groups[1];
                string[] workItemIds = group.Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < workItemIds.Length; i++)
                {
                    string workItemId = workItemIds[i].Trim();

                    if (!string.IsNullOrEmpty(workItemId))
                    {
                        if (workItemId[0] == c_workItemChar)
                        {
                            workItemId = workItemId.Remove(0, 1);
                        }

                        int id;
                        if (int.TryParse(workItemId, out id) == false)
                        {
                            continue;
                        }
                        try
                        {
                            workItemModifier.Associate(id, changesetId);
                            workItemModifier.SetWorkItemFixed(id, changesetId);
                        }
                        catch (Exception e)
                        {
                            // We can't really raise an error here, because
                            // we would fail the commit from the client side, while the changes
                            // were already committed to the source control provider;
                            // since we consider associating with work items nice but not essential,
                            // we will log the error and ignore it.
                            logger.Error("Failed to associate work item with changeset", e);
                        }
                    }
                }
            }
        }

        public virtual byte[] ReadFile(ItemMetaData item)
        {
            return fileRepository.GetFile(item, GetRepositoryUuid());
        }

        public virtual void ReadFileAsync(ItemMetaData item)
        {
            fileRepository.ReadFileAsync(item, GetRepositoryUuid());
        }

        public virtual Guid GetRepositoryUuid()
        {
            string cacheKey = "GetRepositoryUuid_" + serverUrl;
            CachedResult result = cache.Get(cacheKey);
            if (result != null)
                return (Guid)result.Value;
            Guid id = sourceControlService.GetRepositoryId(serverUrl, credentials);
            cache.Set(cacheKey, id);
            return id;
        }

        public virtual int GetVersionForDate(DateTime date)
        {
            date = date.ToUniversalTime();
            try
            {
                ItemSpec itemSpec = CreateItemSpec(rootPath, RecursionType.Full); // SVNBRIDGE_WARNING_REF_RECURSION
                Changeset[] changesets = Service_QueryHistory(
                    itemSpec, VersionSpec.Latest,
                    VersionSpec.First, VersionSpec.FromDate(date),
                    1,
                    false);

                // If no results then date is before project existed
                if (changesets.Length == 0)
                    return 0;

                return changesets[0].cset;
            }
            catch (Exception e)
            {
                if (e.Message.StartsWith("TF14021:")) // Date is before repository started
                    return 0;

                throw;
            }
        }

        public virtual void SetActivityComment(string activityId, string comment)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                activity.Comment = comment;
            });
        }

        public virtual void SetProperty(string activityId, string path, string property, string value)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                if (!activity.Properties.ContainsKey(path))
                {
                    activity.Properties[path] = new Properties();
                }

                activity.Properties[path].Added[property] = value;
            });
        }

        public virtual void RemoveProperty(string activityId, string path, string property)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                if (!activity.Properties.ContainsKey(path))
                {
                    activity.Properties[path] = new Properties();
                }
                activity.Properties[path].Removed.Add(property);
            });
        }

        public virtual bool WriteFile(string activityId, string path, byte[] fileData)
        {
            return WriteFile(activityId, path, fileData, false);
        }

        /// <remarks>
        /// Hmm... this helper is a bit dirty... but it helps.
        /// Should be reworked into a class which assembles an itemPaths member
        /// via various helper methods that return property file names.
        /// </remarks>
        private void CollectItemPaths(
            string path,
            ref List<string> itemPaths,
            Recursion recursion)
        {
            itemPaths.Add(path);

            // shortcut
            if ((recursion != Recursion.None) && (recursion != Recursion.OneLevel))
                return;

            string propertiesForFile = GetPropertiesFileName(path, ItemType.File);
            string propertiesForFolder = GetPropertiesFileName(path, ItemType.Folder);
            string propertiesForFolderItems = path + "/" + Constants.PropFolder;

            if (recursion == Recursion.None)
            {
                if (propertiesForFile.Length <= maxLengthFromRootPath)
                    itemPaths.Add(propertiesForFile);

                if (propertiesForFolder.Length <= maxLengthFromRootPath)
                    itemPaths.Add(propertiesForFolder);
            }
            else if (recursion == Recursion.OneLevel)
            {
                if (propertiesForFile.Length <= maxLengthFromRootPath)
                    itemPaths.Add(propertiesForFile);

                if (propertiesForFolderItems.Length <= maxLengthFromRootPath)
                    itemPaths.Add(propertiesForFolderItems);
            }
        }

        private ItemMetaData GetItems(int version, string path, Recursion recursion, bool returnPropertyFiles)
        {
            // WARNING: this interface will
            // return filename items with a case-insensitive match,
            // due to querying into TFS-side APIs!
            // All users which rely on precise case-sensitive matching
            // will need to account for this.
            // Ideally we should offer a clean interface here
            // which ensures case-sensitive matching when needed.

            SVNPathStripLeadingSlash(ref path);

            if (version == 0 && path == "")
            {
                version = GetEarliestVersion(path);
            }

            if (version == -1)
            {
                version = GetLatestVersion();
            }

            List<string> itemPathsToBeQueried = new List<string>();
            CollectItemPaths(
                path,
                ref itemPathsToBeQueried,
                recursion);

            SourceItem[] items = metaDataRepository.QueryItems(version, itemPathsToBeQueried.ToArray(), recursion);
            if (recursion == Recursion.OneLevel)
            {
                if (items.Length > 0 && items[0].ItemType == ItemType.Folder)
                {
                    List<string> propertiesForSubFolders = new List<string>();
                    foreach (SourceItem item in items)
                    {
                        if (item.ItemType == ItemType.Folder && !IsPropertyFolder(item.RemoteName))
                        {
                            string propertiesForFolder = GetPropertiesFileName(item.RemoteName, ItemType.Folder);
                            if (propertiesForFolder.Length <= maxLengthFromRootPath)
                                propertiesForSubFolders.Add(propertiesForFolder);
                        }
                    }
                    SourceItem[] subFolderProperties = metaDataRepository.QueryItems(version, propertiesForSubFolders.ToArray(), Recursion.None);
                    List<SourceItem> mergedItems = new List<SourceItem>(items);
                    foreach (SourceItem item in subFolderProperties)
                        mergedItems.Add(item);

                    items = mergedItems.ToArray();
                }
            }

            Dictionary<string, FolderMetaData> folders = new Dictionary<string, FolderMetaData>();
            Dictionary<string, ItemProperties> properties = new Dictionary<string, ItemProperties>();
            Dictionary<string, int> itemPropertyRevision = new Dictionary<string, int>();
            ItemMetaData firstItem = null;
            // Workaround variable for case sensitivity issues - always keep a record of the folder name
            // which a (potentially imprecisely named!) file should nevertheless have ended up in.
            // This has been observed with a Changeset where 50 files were correctly named yet
            // 2 others (the elsewhere case-infamous resource.h sisters) had incorrect folder case.
            // While this managed to cure the problem, another, possibly more helpful
            // (in case of some completely unrelated folder place being affected!), way of doing the workaround
            // would be to have a helper method manually iterate through the folder map
            // and do an insensitive string compare to figure out the likely candidate folder.
            string currentFolderName = null;
            foreach (SourceItem sourceItem in items)
            {
                ItemMetaData item = ConvertSourceItem(sourceItem, rootPath);
                if (IsPropertyFile(item.Name) && !returnPropertyFiles)
                {
                    string itemPath = GetItemFileNameFromPropertiesFileName(item.Name);
                    itemPropertyRevision[itemPath] = item.Revision;
                    properties[itemPath] = Helper.DeserializeXml<ItemProperties>(ReadFile(item));
                }
                else if ((!IsPropertyFile(item.Name) && !IsPropertyFolder(item.Name)) || returnPropertyFiles)
                {
                    // FIXME: this optimistic handling relies on a folder-type item always being listed
                    // prior to its file-type content, which may sometimes not be the case.
                    // Might need to rearrange things more flexibly (by using the usual
                    // first-create-StubFolderMetaData-then-replace-with-real-FolderMetaData-item mechanism).
                    if (item.ItemType == ItemType.Folder)
                    {
                        currentFolderName = FilesysHelpers.GetCaseMangledName(item.Name);
                        folders[currentFolderName] = (FolderMetaData)item;
                    }
                    if (firstItem == null)
                    {
                        firstItem = item;
                        if (item.ItemType == ItemType.File)
                        {
                            string folderName = GetFolderName(item.Name);
                            string folderNameMangled = FilesysHelpers.GetCaseMangledName(folderName);
                            folders[folderNameMangled] = new FolderMetaData();
                            folders[folderNameMangled].Items.Add(item);
                        }
                    }
                    else
                    {
                        string folderName = GetFolderName(item.Name);
                        FolderMetaData folder = null;
                        if (!folders.TryGetValue(FilesysHelpers.GetCaseMangledName(folderName), out folder)) // NOT FOUND?? (case sensitivity!?) Try recorded folder.
                        {
                            folder = folders[currentFolderName];
                        }
                        folder.Items.Add(item);
                    }
                }
            }
            SetItemProperties(folders, properties);
            UpdateItemRevisionsBasedOnPropertyItemRevisions(folders, itemPropertyRevision);
            if (!returnPropertyFiles)
            {
                UpdateFolderRevisions(firstItem, version, recursion);
            }
            return firstItem;
        }

        /// <summary>
        /// Small helper to strip off the leading slash
        /// that may be fed from outer users of this interface,
        /// i.e. to be predominantly used in (usually) public methods of this class
        /// (inner methods should try to not need to call this any more,
        /// since public -> private transition is expected
        /// to already have catered for it).
        /// Longer explanation: since leading-slash paths are an SVN protocol characteristic
        /// (see e.g. raw paths passed into PROPFIND request)
        /// and TFSSourceControlProvider is our exact raw SVN-conformant interface,
        /// and ItemMetaData specs are always non-leading-slash,
        /// this is *exactly* the right internal layer to do path stripping.
        /// </summary>
        /// <param name="path">Path value to be processed</param>
        private static void SVNPathStripLeadingSlash(ref string path)
        {
            FilesysHelpers.StripRootSlash(ref path);
        }

        private void UpdateFolderRevisions(ItemMetaData item, int version, Recursion recursion)
        {
            if (item != null && item.ItemType == ItemType.Folder)
            {
                FolderMetaData folder = (FolderMetaData)item;
                foreach (ItemMetaData folderItem in folder.Items)
                {
                    UpdateFolderRevisions(folderItem, version, recursion);
                }
                if (recursion == Recursion.Full)
                {
                    int maxChangeset = int.MinValue;
                    DateTime maxLastModified = DateTime.MinValue;

                    foreach (ItemMetaData folderItem in folder.Items)
                    {
                        if (folderItem.Revision > maxChangeset)
                            maxChangeset = folderItem.Revision;

                        if (folderItem.LastModifiedDate > maxLastModified)
                            maxLastModified = folderItem.LastModifiedDate;
                    }
                    if (maxChangeset > item.ItemRevision)
                        item.SubItemRevision = maxChangeset;

                    if (maxLastModified > item.LastModifiedDate)
                        item.LastModifiedDate = maxLastModified;
                }
                else
                {
                    // SVNBRIDGE_WARNING_REF_RECURSION - additional comments:
                    // the reason for specifying .Full here probably is to get a full history,
                    // which ensures that the first entry (due to sort order)
                    // does provide the *newest* Change(set) anywhere below that item.
                    LogItem log = GetLog(
                        item.Name,
                        version,
                        1,
                        version,
                        Recursion.Full,
                        1);
                    if (log.History.Length != 0)
                    {
                        item.SubItemRevision = log.History[0].ChangeSetID;
                        item.LastModifiedDate = log.History[0].CommitDateTime;
                    }
                }
            }
        }

        /// <summary>
        /// Hotpath performance tweak helper [user code first calls this fast unspecific check,
        /// then iff(!) found (rarely) does more specific ones, whether property file/folder...]
        /// </summary>
        /// <param name="path">item path</param>
        /// <returns>true in case path seems to be a path used for storage of SVN properties, else false</returns>
        private static bool IsSuspectedPropertyStuff(string name)
        {
            return (name.Contains(Constants.PropFolder));
        }

        public bool IsPropertyFile(string name)
        {
            if (IsSuspectedPropertyStuff(name))
            { // found!? --> do precise checks.
                const string propFolderPlusSlash = Constants.PropFolder + "/";
                if (name.StartsWith(propFolderPlusSlash) || name.Contains("/" + propFolderPlusSlash))
                    return true;
            }
            return false;
        }

        public bool IsPropertyFolder(string name)
        {
            if (IsSuspectedPropertyStuff(name))
            { // found!? --> do precise checks.
                if (name == Constants.PropFolder || name.EndsWith("/" + Constants.PropFolder))
                    return true;
            }
            return false;
        }

        public bool IsPropertyFolderElement(string name)
        {
            if (IsSuspectedPropertyStuff(name))
            {
                return (
                    (name.StartsWith(Constants.PropFolder + "/") ||
                     name.EndsWith("/" + Constants.PropFolder) ||
                     name.Contains("/" + Constants.PropFolder + "/"))
                );
            }
            return false;
        }

        private static void UpdateItemRevisionsBasedOnPropertyItemRevisions(IDictionary<string, FolderMetaData> folders, IEnumerable<KeyValuePair<string, int>> itemPropertyRevision)
        {
            foreach (KeyValuePair<string, int> propertyRevision in itemPropertyRevision)
            {
                string propertyKey = propertyRevision.Key;

                string propertyKeyMangled = propertyKey.ToLower();
                if (folders.ContainsKey(propertyKeyMangled))
                {
                    ItemMetaData item = folders[propertyKeyMangled];
                    item.PropertyRevision = propertyRevision.Value;
                }
                else
                {
                    string folderName = GetFolderName(propertyKey).ToLowerInvariant();

                    FolderMetaData folder;
                    if (folders.TryGetValue(folderName, out folder) == false)
                        continue;

                    foreach (ItemMetaData folderItem in folder.Items)
                    {
                        if (folderItem.Name == propertyKey)
                        {
                            folderItem.PropertyRevision = propertyRevision.Value;
                        }
                    }
                }
            }
        }

        private bool IsDeleted(string activityId, string path)
        {
            bool result = false;
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                if (activity.DeletedItems.Contains(path))
                {
                    result = true;
                }
            });
            return result;
        }

        private void RevertDelete(string activityId, string path)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                string serverItemPath = MakeTfsPath(path);
                ServiceUndoPendingRequests(activityId, new string[] { serverItemPath });
                activity.DeletedItems.Remove(path);
                //for (int j = activity.MergeList.Count - 1; j >= 0; j--)
                //{
                //    if (activity.MergeList[j].Action == ActivityItemAction.Deleted
                //        && activity.MergeList[j].Path == serverItemPath)
                //    {
                //        activity.MergeList.RemoveAt(j);
                //    }
                //}
                activity.MergeList.RemoveAll(
                  elem => (elem.Action == ActivityItemAction.Deleted) && (elem.Path == serverItemPath)
                );
            });
        }

        private MergeActivityResponse GenerateMergeResponse(string activityId, int changesetId)
        {
            MergeActivityResponse mergeResponse = new MergeActivityResponse(changesetId, DateTime.Now, "unknown");
            List<string> baseFolders = new List<string>();
            List<string> sortedMergeResponse = new List<string>();
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                foreach (ActivityItem item in activity.MergeList)
                {
                    ActivityItem newItem = item;
                    if (!IsPropertyFolder(item.Path))
                    {
                        if (IsPropertyFile(item.Path))
                        {
                            string path = item.Path.Replace("/" + Constants.PropFolder + "/", "/");
                            ItemType newItemType = item.FileType;
                            if (path.EndsWith("/" + Constants.FolderPropFile))
                            {
                                path = path.Replace("/" + Constants.FolderPropFile, "");
                                newItemType = ItemType.Folder;
                            }
                            newItem = new ActivityItem(path, newItemType, item.Action);
                        }

                        if (!sortedMergeResponse.Contains(newItem.Path))
                        {
                            sortedMergeResponse.Add(newItem.Path);

                            string path = newItem.Path.Substring(rootPath.Length - 1);
                            if (path == "")
                                path = "/";

                            MergeActivityResponseItem responseItem =
                                new MergeActivityResponseItem(newItem.FileType, path);
                            if (newItem.Action != ActivityItemAction.Deleted && newItem.Action != ActivityItemAction.Branch &&
                                newItem.Action != ActivityItemAction.RenameDelete)
                            {
                                mergeResponse.Items.Add(responseItem);
                            }

                            AddBaseFolderIfRequired(activityId, newItem, baseFolders, mergeResponse);
                        }
                    }
                }
            });
            return mergeResponse;
        }

        private void AddBaseFolderIfRequired(string activityId, ActivityItem item, ICollection<string> baseFolders, MergeActivityResponse mergeResponse)
        {
            string folderName = GetFolderName(item.Path);
            if (((item.Action == ActivityItemAction.New) || (item.Action == ActivityItemAction.Deleted) ||
                 (item.Action == ActivityItemAction.RenameDelete)) && !baseFolders.Contains(folderName))
            {
                baseFolders.Add(folderName);
                bool folderFound = false;

                ActivityRepository.Use(activityId, delegate(Activity activity)
                {
                    foreach (ActivityItem folderItem in activity.MergeList)
                    {
                        if (folderItem.FileType == ItemType.Folder && folderItem.Path == folderName)
                        {
                            folderFound = true;
                        }
                    }
                });

                if (!folderFound)
                {
                    folderName = GetFolderName(item.Path.Substring(rootPath.Length));
                    if (!folderName.StartsWith("/"))
                        folderName = "/" + folderName;
                    MergeActivityResponseItem responseItem = new MergeActivityResponseItem(ItemType.Folder, folderName);
                    mergeResponse.Items.Add(responseItem);
                }
            }
        }

        private bool WriteFile(string activityId, string path, byte[] fileData, bool reportUpdatedFile)
        {
            bool replaced = false;
            if (IsDeleted(activityId, path))
            {
                replaced = true;
                RevertDelete(activityId, path);
            }
            bool newFile = true;

            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                ItemMetaData item;
                string existingPath = path.Substring(1);

                do
                {
                    int lastIndexOf = existingPath.LastIndexOf('/');
                    if (lastIndexOf != -1)
                        existingPath = existingPath.Substring(0, lastIndexOf);
                    else
                        existingPath = "";

                    item = GetItems(-1, existingPath, Recursion.None, true);
                } while (item == null);

                string localPath = GetLocalPath(activityId, path);
                List<LocalUpdate> updates = new List<LocalUpdate>();
                updates.Add(LocalUpdate.FromLocal(item.Id,
                                                  localPath.Substring(0, localPath.LastIndexOf('\\')),
                                                  item.Revision));

                item = GetItems(-1, path.Substring(1), Recursion.None, true);
                if (item != null)
                {
                    updates.Add(LocalUpdate.FromLocal(item.Id, localPath, item.Revision));
                }

                ServiceUpdateLocalVersions(activityId, updates);

                List<PendRequest> pendRequests = new List<PendRequest>();

                bool addToMergeList = true;
                if (item != null)
                {
                    pendRequests.Add(PendRequest.Edit(localPath));
                    newFile = false;
                }
                else
                {
                    ItemMetaData pendingItem = GetPendingItem(activityId, path);
                    if (pendingItem == null)
                    {
                        pendRequests.Add(PendRequest.AddFile(localPath, TfsUtil.CodePage_ANSI));
                    }
                    else
                    {
                        UpdateLocalVersion(activityId, pendingItem, localPath);
                        pendRequests.Add(PendRequest.Edit(localPath));
                        newFile = false;
                    }
                    foreach (CopyAction copy in activity.CopiedItems)
                    {
                        if (copy.TargetPath == path)
                        {
                            addToMergeList = false;
                        }
                    }
                }

                ServiceSubmitPendingRequests(activityId, pendRequests);
                string pathFile = MakeTfsPath(path);
                sourceControlService.UploadFileFromBytes(serverUrl, credentials, activityId, fileData, pathFile);

                if (addToMergeList)
                {
                    if (!replaced && (!newFile || reportUpdatedFile))
                    {
                        activity.MergeList.Add(new ActivityItem(pathFile, ItemType.File, ActivityItemAction.Updated));
                    }
                    else
                    {
                        activity.MergeList.Add(new ActivityItem(pathFile, ItemType.File, ActivityItemAction.New));
                    }
                }
            });

            return newFile;
        }

        private void UndoPendingRequests(string activityId, Activity activity, string path)
        {
            ServiceUndoPendingRequests(activityId,
                                       new string[] { path });
            //for (int i = activity.MergeList.Count - 1; i >= 0; i--)
            //{
            //    if (activity.MergeList[i].Path == path)
            //    {
            //        activity.MergeList.RemoveAt(i);
            //    }
            //}
            activity.MergeList.RemoveAll(elem => (elem.Path == path));
        }

        private void ConvertCopyToRename(string activityId, CopyAction copy)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                string pathTargetFull = MakeTfsPath(copy.TargetPath);
                UndoPendingRequests(activityId, activity, pathTargetFull);

                ProcessCopyItem(activityId, copy, true);
            });
        }

        /// <summary>
        /// Returns a full TFS path (combination of rootPath plus the item's sub path).
        /// </summary>
        /// <param name="itemPath">sub path of the item</param>
        /// <returns>combined/full TFS path to item</returns>
        private string MakeTfsPath(string itemPath)
        {
            return Helper.CombinePath(rootPath, itemPath);
        }

        private static string GetLocalPath(string activityId, string path)
        {
            return Constants.LocalPrefix + activityId + path.Replace('/', '\\');
        }

        private void UpdateLocalVersion(string activityId, ItemMetaData item, string localPath)
        {
            UpdateLocalVersion(activityId, item.Id, item.ItemRevision, localPath);
        }

        private void UpdateLocalVersion(string activityId, int itemId, int itemRevision, string localPath)
        {
            List<LocalUpdate> updates = new List<LocalUpdate>();
            updates.Add(LocalUpdate.FromLocal(itemId, localPath, itemRevision));
            ServiceUpdateLocalVersions(activityId, updates);
        }

        private void ServiceUpdateLocalVersions(string activityId, IEnumerable<LocalUpdate> updates)
        {
            sourceControlService.UpdateLocalVersions(serverUrl, credentials, activityId, updates);
        }

        private void ProcessCopyItem(string activityId, CopyAction copyAction, bool forceRename)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                string localPath = GetLocalPath(activityId, copyAction.Path);
                string localTargetPath = GetLocalPath(activityId, copyAction.TargetPath);

                bool copyIsRename = false;
                if (IsDeleted(activityId, copyAction.Path))
                {
                    copyIsRename = true;
                    RevertDelete(activityId, copyAction.Path);
                }
                ItemMetaData item = GetItemsWithoutProperties(-1, copyAction.Path, Recursion.None);
                UpdateLocalVersion(activityId, item, localPath);

                if (copyIsRename)
                {
                    activity.MergeList.Add(new ActivityItem(MakeTfsPath(copyAction.Path), item.ItemType, ActivityItemAction.RenameDelete));
                }

                if (!copyIsRename)
                {
                    foreach (CopyAction copy in activity.CopiedItems)
                    {
                        if (copyAction.Path.StartsWith(copy.Path + "/"))
                        {
                            string path = copy.TargetPath + copyAction.Path.Substring(copy.Path.Length);
                            for (int i = activity.DeletedItems.Count - 1; i >= 0; i--)
                            {
                                if (activity.DeletedItems[i] == path)
                                {
                                    copyIsRename = true;

                                    string pathDeletedFull = MakeTfsPath(activity.DeletedItems[i]);
                                    UndoPendingRequests(activityId, activity, pathDeletedFull);

                                    activity.DeletedItems.RemoveAt(i);

                                    localPath = GetLocalPath(activityId, path);
                                    ItemMetaData pendingItem = GetPendingItem(activityId, path);
                                    UpdateLocalVersion(activityId, pendingItem, localPath);
                                }
                            }
                        }
                    }
                }
                if (!copyIsRename)
                {
                    for (int i = activity.DeletedItems.Count - 1; i >= 0; i--)
                    {
                        if (copyAction.Path.StartsWith(activity.DeletedItems[i] + "/"))
                        {
                            copyIsRename = true;
                            activity.PostCommitDeletedItems.Add(activity.DeletedItems[i]);

                            string pathDeletedFull = MakeTfsPath(activity.DeletedItems[i]);
                            UndoPendingRequests(activityId, activity, pathDeletedFull);

                            activity.DeletedItems.RemoveAt(i);
                        }
                    }
                }
                if (!copyIsRename)
                {
                    foreach (string deletedItem in activity.PostCommitDeletedItems)
                    {
                        if (copyAction.Path.StartsWith(deletedItem + "/"))
                        {
                            copyIsRename = true;
                        }
                    }
                }

                PendRequest pendRequest = null;
                PendRequest pendRequestPending = null;
                if (copyIsRename || forceRename)
                {
                    if (IsDeleted(activityId, copyAction.TargetPath))
                    {
                        activity.PendingRenames[localTargetPath] = PendRequest.Rename(localPath, localTargetPath);
                    }
                    else
                    {
                        pendRequest = PendRequest.Rename(localPath, localTargetPath);
                        if (activity.PendingRenames.ContainsKey(localPath))
                        {
                            pendRequestPending = activity.PendingRenames[localPath];
                            activity.PendingRenames.Remove(localPath);
                        }
                    }
                    copyAction.Rename = true;
                }
                else
                {
                    pendRequest = PendRequest.Copy(localPath, localTargetPath);
                }
                if (pendRequest != null)
                {
                    ServiceSubmitPendingRequest(activityId, pendRequest);
                    UpdateLocalVersion(activityId, item, localTargetPath);
                    if (pendRequestPending != null)
                    {
                        ServiceSubmitPendingRequest(activityId, pendRequestPending);
                    }
                }
                string pathCopyTarget = MakeTfsPath(copyAction.TargetPath);
                if (copyAction.Rename)
                {
                    activity.MergeList.Add(
                        new ActivityItem(pathCopyTarget, item.ItemType, ActivityItemAction.New));
                }
                else
                {
                    activity.MergeList.Add(
                        new ActivityItem(pathCopyTarget, item.ItemType, ActivityItemAction.Branch,
                            MakeTfsPath(copyAction.Path)));
                }
            });
        }

        private static string GetPropertiesFolderName(string path, ItemType itemType)
        {
            if (itemType == ItemType.Folder)
            {
                if (path == "/")
                    return "/" + Constants.PropFolder;
                return path + "/" + Constants.PropFolder;
            }
            if (path.LastIndexOf('/') != -1)
                return path.Substring(0, path.LastIndexOf('/')) + "/" + Constants.PropFolder;
            return Constants.PropFolder;
        }

        private static string GetItemFileNameFromPropertiesFileName(string path)
        {
            string itemPath = path;
            if (itemPath == Constants.PropFolder + "/" + Constants.FolderPropFile)
            {
                itemPath = "";
            }
            else if (itemPath.StartsWith(Constants.PropFolder + "/"))
            {
                itemPath = path.Substring(Constants.PropFolder.Length + 1);
            }
            else
            {
                itemPath = itemPath.Replace("/" + Constants.PropFolder + "/" + Constants.FolderPropFile, "");
                itemPath = itemPath.Replace("/" + Constants.PropFolder + "/", "/");
            }
            return itemPath;
        }

        private static string GetPropertiesFileName(string path, ItemType itemType)
        {
            if (itemType == ItemType.Folder)
            {
                if (path == "/")
                    return "/" + Constants.PropFolder + "/" + Constants.FolderPropFile;
                return path + "/" + Constants.PropFolder + "/" + Constants.FolderPropFile;
            }
            if (path.LastIndexOf('/') != -1)
            {
                return
                    path.Substring(0, path.LastIndexOf('/')) + "/" + Constants.PropFolder +
                    path.Substring(path.LastIndexOf('/'));
            }
            return Constants.PropFolder + "/" + path;
        }

        private void ProcessDeleteItem(string activityId, string path)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                string localPath = GetLocalPath(activityId, path);

                ItemMetaData item = GetItems(-1, path, Recursion.None, true);
                if (item == null)
                {
                    item = GetPendingItem(activityId, path);
                }

                UpdateLocalVersion(activityId, item, localPath);

                if (item.ItemType != ItemType.Folder)
                {
                    string propertiesFile = GetPropertiesFileName(path, item.ItemType);
                    DeleteItem(activityId, propertiesFile);
                }

                ServiceSubmitPendingRequest(activityId, PendRequest.Delete(localPath));

                activity.MergeList.Add(new ActivityItem(MakeTfsPath(path), item.ItemType, ActivityItemAction.Deleted));

            });
        }

        private ItemProperties ReadPropertiesForItem(string path, ItemType itemType)
        {
            ItemProperties properties = null;
            string propertiesPath = GetPropertiesFileName(path, itemType);
            string cacheKey = "ReadPropertiesForItem_" + propertiesPath;
            ItemMetaData item;
            CachedResult cachedResult = cache.Get(cacheKey);

            if (cachedResult == null)
            {
                item = GetItems(-1, propertiesPath, Recursion.None, true);
                cache.Set(cacheKey, item);
            }
            else
            {
                item = (ItemMetaData)cachedResult.Value;
            }

            if (item != null)
            {
                properties = Helper.DeserializeXml<ItemProperties>(ReadFile(item));
            }
            return properties;
        }

        private void UpdateProperties(string activityId)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                ItemMetaData item;
                ItemType itemType;

                Dictionary<string, Property> propertiesToAdd = new Dictionary<string, Property>();
                foreach (string path in activity.Properties.Keys)
                {
                    ItemProperties properties = GetItemProperties(activity, path, out item, out itemType);
                    foreach (Property property in properties.Properties)
                    {
                        propertiesToAdd[property.Name] = property;
                    }
                    foreach (KeyValuePair<string, string> property in activity.Properties[path].Added)
                    {
                        propertiesToAdd[property.Key] = new Property(property.Key, property.Value);
                    }
                    foreach (string removedProperty in activity.Properties[path].Removed)
                    {
                        propertiesToAdd.Remove(removedProperty);
                    }
                    string propertiesPath = GetPropertiesFileName(path, itemType);
                    string propertiesFolder = GetPropertiesFolderName(path, itemType);
                    ItemMetaData propertiesFolderItem = GetItems(-1, propertiesFolder, Recursion.None, true);
                    if ((propertiesFolderItem == null) && !activity.Collections.Contains(propertiesFolder))
                    {
                        MakeCollection(activityId, propertiesFolder);
                    }

                    properties.Properties.AddRange(propertiesToAdd.Values);

                    if (item != null)
                    {
                        WriteFile(activityId, propertiesPath, Helper.SerializeXml(properties), true);
                    }
                    else
                    {
                        WriteFile(activityId, propertiesPath, Helper.SerializeXml(properties));
                    }
                }
            });
        }

        private ItemProperties GetItemProperties(Activity activity, string path, out ItemMetaData item, out ItemType itemType)
        {
            itemType = ItemType.File;
            item = GetItems(-1, path, Recursion.None);
            if (item != null)
            {
                itemType = item.ItemType;
            }
            else if (activity.Collections.Contains(path))
            {
                itemType = ItemType.Folder;
            }

            ItemProperties properties = ReadPropertiesForItem(path, itemType);
            if (properties == null)
            {
                properties = new ItemProperties();
            }
            return properties;
        }

        private static string GetFolderName(string path)
        {
            string folderName = "";
            if (path.Contains("/"))
            {
                folderName = path.Substring(0, path.LastIndexOf('/'));
            }
            return folderName;
        }

        private ItemMetaData GetPendingItem(string activityId, string path)
        {
            ItemSpec spec = new ItemSpec();
            spec.item = MakeTfsPath(path);
            ExtendedItem[][] items =
                sourceControlService.QueryItemsExtended(serverUrl,
                                                        credentials,
                                                        activityId,
                                                        new ItemSpec[1] { spec },
                                                        DeletedState.NonDeleted,
                                                        ItemType.Any,
                                                        0);
            if (items[0].Length == 0)
                return null;
            ItemMetaData pendingItem = new ItemMetaData();
            if (items[0][0].type == ItemType.Folder)
            {
                pendingItem = new FolderMetaData();
            }

            pendingItem.Id = items[0][0].itemid;
            pendingItem.ItemRevision = items[0][0].latest;
            return pendingItem;
        }

        private void SetItemProperties(IDictionary<string, FolderMetaData> folders, IEnumerable<KeyValuePair<string, ItemProperties>> properties)
        {
            foreach (KeyValuePair<string, ItemProperties> itemProperties in properties)
            {
                ItemMetaData item = null;
                string key = itemProperties.Key.ToLowerInvariant();
                if (folders.ContainsKey(key))
                {
                    item = folders[key];
                }
                else
                {
                    string folderName = GetFolderName(itemProperties.Key)
                        .ToLowerInvariant();
                    if (folders.ContainsKey(folderName))
                    {
                        item = folders[folderName].FindItem(itemProperties.Key);
                    }
                }
                if (item != null)
                {
                    foreach (Property property in itemProperties.Value.Properties)
                    {
                        item.Properties[property.Name] = property.Value;
                    }
                }
            }
        }

        public virtual ItemMetaData[] GetPreviousVersionOfItems(SourceItem[] items, int changeset)
        {
            // Processing steps:
            // - given the this-changeset source items,
            //   figure out the corresponding maximally-authoritative representation (numeric IDs) of these items
            // - do a QueryItems() with these IDs, on the *previous* changeset

            var branchQueries = sourceControlService.QueryBranches(serverUrl, 
                                                                   credentials,
                                                                   items.Select(item => CreateItemSpec(MakeTfsPath(item.RemoteName), RecursionType.None)).ToArray(), 
                                                                   VersionSpec.FromChangeset(changeset));
            var renamedItems = items.Select((item, i) =>
                branchQueries[i].FirstOrDefault(branchItem => 
                    branchItem.ToItem != null && 
                    branchItem.ToItem.RemoteChangesetId == changeset && 
                    branchItem.ToItem.RemoteName == MakeTfsPath(item.RemoteName))).ToList();
            
            var previousRevision = changeset - 1;

            if (renamedItems.All(item => item == null || item.FromItem == null))
            {
                // fallback for TFS08 and earlier
                var previousSourceItems = sourceControlService.QueryItems(serverUrl, credentials,
                    items.Select(item => item.ItemId).ToArray(),
                    previousRevision,
                    0);
                return previousSourceItems.Select(sourceItem => ConvertSourceItem(sourceItem, rootPath)).ToArray();
            }

            var result = new List<ItemMetaData>();
            for (var i = 0; i < renamedItems.Count; i++)
            {
                var previousSourceItemId = (renamedItems[i] != null && renamedItems[i].FromItem != null) ? renamedItems[i].FromItem.ItemId : items[i].ItemId;
                var previousSourceItems = sourceControlService.QueryItems(serverUrl, credentials,
                    new[] { previousSourceItemId },
                    previousRevision,
                    0);
                result.Add(previousSourceItems.Length > 0 ? ConvertSourceItem(previousSourceItems[0], rootPath) : null);
            }
            return result.ToArray();
        }

        private static ItemSpec CreateItemSpec(string item, RecursionType recurse)
        {
            return new ItemSpec { item = item, recurse = recurse };
        }

        public virtual int GetEarliestVersion(string path)
        {
            LogItem log = GetLog(
                path,
                1,
                GetLatestVersion(),
                Recursion.None,
                int.MaxValue);
            return log.History[log.History.Length - 1].ChangeSetID;
        }

        // TODO: these helpers should perhaps eventually be moved
        // into a helper class (SourceControlSession?)
        // which encapsulates sourceControlService, serverUrl, credentials members,
        // as a member of this provider class,
        // thereby simplifying common invocations.
        private void ServiceSubmitPendingRequest(string activityId, PendRequest pendRequest)
        {
            List<PendRequest> pendRequests = new List<PendRequest>();
            pendRequests.Add(pendRequest);
            ServiceSubmitPendingRequests(activityId, pendRequests);
        }

        /// <summary>
        /// Will register (stage) file changes in our temporary TFS workspace
        /// as ready for commit.
        /// This is aka TFS Pending Changes,
        /// which can actually be seen in Visual Studio Source Control Explorer
        /// as filed in its proper SvnBridge-created temporary Workspace ID
        /// while debugging (but note that at least on VS10+TFS08,
        /// Workspace status gets refreshed rather very lazily -
        /// use Context Menu -> Refresh to force a refresh to current status).
        /// TFS error message in case Pending Changes were not done correctly:
        /// "The item" ... "could not be found in your workspace."
        ///
        /// This is a queueing-only operation - final atomic commit transaction
        /// of all these elements queued within this activity
        /// will then happen on invocation of .Commit().
        /// </summary>
        /// <param name="activityId">ID of the activity to file these requests under</param>
        /// <param name="pendRequests">list of pending requests (TFS Pending Changes)</param>
        private void ServiceSubmitPendingRequests(string activityId, IEnumerable<PendRequest> pendRequests)
        {
            // Watch all pending items submitted for the TFS-side transaction / workspace
            // during an entire SVN-side WebDAV
            // MKACTIVITY
            //   ... CHECKOUT / COPY / PUT / DELETE ...
            // DELETE
            // transaction lifecycle:
            Helper.DebugUsefulBreakpointLocation();

            sourceControlService.PendChanges(
                serverUrl, credentials,
                activityId, pendRequests,
                0, 0
            );
        }

        /// <summary>
        /// Will undo file changes in our temporary TFS workspace
        /// for an item.
        /// Note that this unfortunately is limited to undoing
        /// *all* pending changes of a path,
        /// which might turn out to be a problem in case of
        /// *multiple* prior Pending Changes registered for one item.
        /// </summary>
        /// <param name="activityId">ID of the activity to file these requests under</param>
        /// <param name="pendRequests">list of items to have all their Pending Changes undone</param>
        private void ServiceUndoPendingRequests(string activityId, IEnumerable<string> serverItems)
        {
            sourceControlService.UndoPendingChanges(
                serverUrl, credentials,
                activityId, serverItems
            );
        }

        private ItemMetaData ConvertSourceItem(SourceItem sourceItem, string rootPath)
        {
            ItemMetaData item;
            if (sourceItem.ItemType == ItemType.Folder)
            {
                item = new FolderMetaData();
            }
            else
            {
                item = new ItemMetaData();
            }

            item.Id = sourceItem.ItemId;
            if (rootPath.Length <= sourceItem.RemoteName.Length)
            {
                item.Name = sourceItem.RemoteName.Substring(rootPath.Length);
            }
            else
            {
                item.Name = "";
            }

            item.Author = "unknown";
            item.LastModifiedDate = sourceItem.RemoteDate;
            item.ItemRevision = sourceItem.RemoteChangesetId;
            item.DownloadUrl = sourceItem.DownloadUrl;
            return item;
        }
    }
}
