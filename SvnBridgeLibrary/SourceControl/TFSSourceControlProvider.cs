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
        private const string repo_separator_s = "/";
        private const char repo_separator_c = '/';

        public static void StripRootSlash(ref string path)
        {
            if (path.StartsWith(repo_separator_s))
                path = path.Substring(1);
        }

        public static string StripBasePath(string name, string basePath)
        {
            StripRootSlash(ref name);

            StripRootSlash(ref basePath);

            basePath = basePath + "/";

            if (name.StartsWith(basePath))
            {
                name = name.Substring(basePath.Length);
                StripRootSlash(ref name);
            }
            return name;
        }

        // A helper not unlike UNIX "dirname"
        // (albeit for filename-only arguments it will return "" rather than ".").
        public static string GetFolderPathPart(string path)
        {
            string folderName = "";
            var idxLastSep = path.LastIndexOf(repo_separator_c);
            bool haveLastSep = (-1 != idxLastSep);
            if (haveLastSep)
            {
                folderName = path.Substring(0, idxLastSep);
            }
            return folderName;
        }

        public static string StripPrefix(string prefix, string full)
        {
            string res = (full.Length > prefix.Length) ? full.Substring(prefix.Length) : "";
            return res;
        }

        public static void PathAppendElem(ref string path, string pathElem)
        {
            if (path != "" && !path.EndsWith(repo_separator_s))
                path += repo_separator_s + pathElem;
            else
                path += pathElem;
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

    internal static class CollectionHelpers
    {
        /// <summary>
        /// Helper method for case-*insensitive* comparison of paths:
        /// manually iterate through the folder map
        /// and do an insensitive string compare to figure out the likely candidate folder.
        /// </summary>
        public static FolderMetaData FindMatchingExistingFolderCandidate_CaseInsensitive(Dictionary<string, FolderMetaData> dict, string folderName)
        {
            // To achieve a case-insensitive comparison, we
            // unfortunately need to manually *iterate* over all hash entries:
            foreach (var pair in dict)
            {
                //if (pair.Key.ToLowerInvariant().Contains("somefile.h"))
                //{
                //    Helper.DebugUsefulBreakpointLocation();
                //}

                // Make sure to also use the method that's commonly used
                // for such path comparison purposes.
                // And do explicitly call the *insensitive* method (i.e. not IsSamePath()),
                // independent of whether .wantCaseSensitiveMatch is set
                // (this is a desperate last-ditch attempt, thus we explicitly do want insensitive).
                if (ItemMetaData.IsSamePathCaseInsensitive(folderName, pair.Key))
                    return pair.Value;
            }
            return null;
        }
    }

    [Interceptor(typeof(TracingInterceptor))]
    [Interceptor(typeof(RetryOnExceptionsInterceptor<SocketException>))]
    public class TFSSourceControlProvider : MarshalByRefObject
    {
        private static readonly Regex s_associatedWorkItems = new Regex(@"(?:(?:(?:fixe?|close|resolve)(?:s|d)?)|(?:Work ?Items?))(?: |:|: )(#?\d+(?:, ?#?\d+)*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);
        private const char c_workItemChar = '#';

        private readonly TFSSourceControlService sourceControlService;
        private readonly string serverUrl;
        private readonly ICredentials credentials;
        private readonly string rootPath;
        private readonly int maxLengthFromRootPath;
        private readonly IWorkItemModifier workItemModifier;
        private readonly DefaultLogger logger;
        private readonly WebCache cache;
        private readonly IMetaDataRepository metaDataRepository;
        private readonly FileRepository fileRepository;
        private const string repoLatestVersion = "Repository.Latest.Version";
        private const string propFolderPlusSlash = Constants.PropFolder + "/";
        // TODO: LATEST_VERSION is an interface-related magic value,
        // thus it should obviously not be within this specific *implementation* class
        // but rather be provided by a corresponding *interface* or base class.
        public const int LATEST_VERSION = -1;

        public TFSSourceControlProvider(
            TFSSourceControlService sourceControlService,
            string serverUrl,
            ICredentials credentials,
            string projectName,
            IWorkItemModifier workItemModifier,
            DefaultLogger logger,
            WebCache cache,
            FileRepository fileRepository)
        {
            this.sourceControlService = sourceControlService;
            this.serverUrl = serverUrl;
            this.credentials = CredentialsHelper.GetCredentialsForServer(this.serverUrl, credentials);

            // NOTE: currently all uses of this class are short-lived and frequent,
            // thus ctor should remain sufficiently *fast*.

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

            this.workItemModifier = workItemModifier;
            this.logger = logger;
            this.cache = cache;

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

            this.fileRepository = fileRepository;
        }

        /// <summary>
        /// The main public interface handler for WebDAV COPY request.
        /// </summary>
        /// <param name="activityId">ID of the activity (transaction)</param>
        /// <param name="versionFrom">The version of the originating item</param>
        /// <param name="path">Location of originating item</param>
        /// <param name="targetPath">Location of destination item</param>
        /// <param name="overwrite">Specifies whether overwriting an existing item is allowed</param>
        public virtual void CopyItem(string activityId, int versionFrom, string path, string targetPath, bool overwrite)
        {
            // CopyAction is not capable of recording a version number that an item has been copied from,
            // thus I assume that a Copy operation is about currently *existing* items only:
            // "copying" == "file existing in HEAD to other file"
            // "writing" == "foreign-revision file to new file (regardless of whether same-path or different-path)"

            // I'm not really happy with this method layering / implementation - it's not very symmetric.
            // But as a first guess it's ok I... guess. ;)
            // FIXME: I'm also not sure about that revision handling here:
            // the source file might have been created at an older revision yet still *exists* currently -
            // we quite likely don't handle this properly here...
            // All in all I'm still feeling very uncertain
            // about how and what we're doing here...

            // Query both magic placeholder for "latest version" *and* do actual latest version HEAD value verify.
            bool copy_head_item = ((LATEST_VERSION == versionFrom) || (GetLatestVersion() == versionFrom));
            copy_head_item = true; // hotfix (branch below IS BROKEN, NEEDS FIXING!!! - probably some transaction management issue in WriteFile() used below)
            if (copy_head_item)
            {
                CopyAction copyAction = new CopyAction(path, targetPath, false);
                ActivityRepository.Use(activityId, delegate(Activity activity)
                {
                    activity.CopiedItems.Add(copyAction);
                });
                // FIXME: obey overwrite param!
                ProcessCopyItem(activityId, versionFrom, copyAction, false);
            }
            else
            {
                // This implements handling for e.g. TortoiseSVN "Revert changes from this revision" operation
                // as described by tracker #15317.
                ItemMetaData itemDestination = GetItemsWithoutProperties(LATEST_VERSION, targetPath, Recursion.None);
                bool can_write = ((null == itemDestination) || (overwrite));
                if (can_write)
                {
                    ItemMetaData itemSource = GetItemsWithoutProperties(versionFrom, path, Recursion.None);
                    byte[] sourceData = ReadFile(itemSource);
                    bool reportUpdatedFile = (null != itemDestination);

                    CopyAction copyAction = new CopyAction(path, targetPath, false);
                    ActivityRepository.Use(activityId, delegate(Activity activity)
                    {
                        activity.CopiedItems.Add(copyAction);
                    });

                    // FIXME: in case of a formerly deleted file, this erases all former file history
                    // due to adding a new file! However, a native-interface undelete operation on TFS2008
                    // (which could be said to be similar in its outcome to this operation in certain situations)
                    // *does* preserve history and gets logged as an Undelete
                    // (not to mention that doing this on an actual SVN is a copy *with* history, too!).
                    // I'm unsure whether we can massage things to have it improved,
                    // especially since flagging things as an Undelete seems to be out of reach in our API.
                    WriteFile(activityId, targetPath, sourceData, reportUpdatedFile);
                }
            }
        }

        public virtual void CopyItem(string activityId, string path, string targetPath)
        {
            CopyItem(activityId, LATEST_VERSION, path, targetPath, true);
        }

        public virtual bool DeleteItem(string activityId, string path)
        {
            if ((GetItems(LATEST_VERSION, path, Recursion.None, true) == null) && (GetPendingItem(activityId, path) == null))
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

        /// <remarks>
        /// Would be nice to be able to get rid of the very SVN-specific
        /// UpdateReportData method param dependency
        /// (this is the sole reason for the Protocol assembly dependency here),
        /// but since UpdateDiffCalculator below depends on it as well
        /// it's not possible even mid-term.
        /// OTOH, one could (rather strongly) argue
        /// that this entire bloated-interface method
        /// is somewhat misplaced within the provider class
        /// and should thus be external to it.
        /// OTOH this probably is done here to do a favour
        /// to the many tests that depend on it
        /// (and make use of the provider as their central object under test).
        /// So, perhaps do keep a "changed items" method after all
        /// and eventually decide to convert it to using a non-SVN update info class.
        /// </remarks>
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
                // FIXME: this one quite likely is WRONG (does not handle subpath expressions
                // of UpdateTarget - checks one hierarchy level only!).
                // Should be using a common UpdateTarget infrastructure helper like other
                // places which need that.
                // Hmm, and <see cref="UpdateReportService"/> implements a GetSrcPath()
                // (combines .SrcPath with .UpdateTarget), whereas we don't use that here -
                // but maybe possibly we should?
                // Well, ok, our *caller* (GetMetadataForUpdate(), i.e. *one* caller at least)
                // did determine path via .SrcPath after all,
                // but that kind of handling is terribly asymmetric :(
                // (evaluating reportData stuff outside *and* then here again)
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
            return GetItemsWithoutProperties(LATEST_VERSION, path, Recursion.None);
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
            var cacheItem = RequestCache.Items[repoLatestVersion];
            if (cacheItem == null)
            {
                cacheItem = sourceControlService.GetLatestChangeset(serverUrl, credentials);
                RequestCache.Items[repoLatestVersion] = cacheItem;
            }
            return (int)cacheItem;
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
                LATEST_VERSION,
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
            if (itemVersion != LATEST_VERSION)
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
                    change.Item.RemoteName = FilesysHelpers.StripPrefix(rootPath, change.Item.RemoteName);

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
                    // Keep using the query method variant that's providing *ItemMetaData*-based results here -
                    // this generically SVN-side handling (after all we're shuffling things according to expressly SVN-side protocol requirements!)
                    // should avoid keeping messing with TfsLibrary-side API dependency types *as much as possible*,
                    // thus getting SourceItem-typed array results is undesirable.
                    // [with the problem remaining
                    // that we then keep working on TfsLibrary-side types
                    // such as SourceItemHistory... oh well].
                    //
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

                    history.Changes.RemoveAll(change => change.ChangeType == ChangeType.None);
                    history.Changes.RemoveAll(change => change.ChangeType == ChangeType.Delete &&
                                              oldItems.Any(oldItem => oldItem != null && oldItem.Id == change.Item.ItemId));
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
                                            // The branching actions history table of an item
                                            // lists *all* actions of that item,
                                            // no matter whether:
                                            // actual branching, or renaming, or initial creation...
                                            bool newlyAdded = (null == branchItem.BranchFromItem);
                                            bool bRenamed = (!newlyAdded);
                                            if (bRenamed)
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
                                    break;
                                }
                            }
                            if (!itemFileIncludedInChanges)
                            {
                                if (change.Item.item.EndsWith(propFolderPlusSlash + Constants.FolderPropFile))
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

                    changesets = null; // GC (large obj / long-running op)

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
            return ItemExists(path, LATEST_VERSION);
        }

        public virtual bool ItemExists(string path, int version)
        {
            bool itemExists = false;

            // Decide to do strip-slash at the very top, since otherwise it would be
            // done *both* by GetItems() internally (its inner copy of the variable)
            // *and* below, by ItemMetaData implementation.
            SVNPathStripLeadingSlash(ref path);
            bool returnPropertyFiles = true;
            ItemMetaData item = GetItems(version, path, Recursion.None, returnPropertyFiles);
            if (item != null)
            {
                itemExists = true;
                bool needCheckCaseSensitiveItemMatch = (Configuration.SCMWantCaseSensitiveItemMatch);
                if (needCheckCaseSensitiveItemMatch)
                {
                    // If the result item is a folder,
                    // then we'll have to do a hierarchy lookup,
                    // otherwise (single file), then we can do a direct compare.
                    if (ItemType.Folder == item.ItemType)
                    {
                        FolderMetaData folder = (FolderMetaData)item;
                        itemExists = (null != folder.FindItem(path));
                    }
                    else
                    {
                        // Comparison for path being data item vs. result being property storage item
                        // would fail, thus we need to do an additional comparison against data item path where needed:
                        itemExists = false;
                        bool haveCorrectlyCasedItem = item.Name.Equals(path);
                        if (haveCorrectlyCasedItem)
                            itemExists = true;
                        if (!itemExists)
                        {
                            bool itemMightBePropStorageItem = (returnPropertyFiles);
                            if (itemMightBePropStorageItem)
                            {
                                string itemPropPath = GetPropertiesFileName(path, item.ItemType);
                                bool haveCorrectlyCasedItem_Prop = item.Name.Equals(itemPropPath);
                                if (haveCorrectlyCasedItem_Prop)
                                    itemExists = true;
                            }
                        }
                    }
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

        public virtual void DeleteActivity(string activityId)
        {
            sourceControlService.DeleteWorkspace(serverUrl, credentials, activityId);
            ActivityRepository.Delete(activityId);
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
                DeleteActivity(workspace.Name);
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

                item = GetItemsWithoutProperties(LATEST_VERSION, existingPath, Recursion.None);
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
                        DoAssociate(id, changesetId);
                    }
                }
            }
        }

        private void DoAssociate(int workItemId, int changesetId)
        {
            try
            {
                string username = GetUsername();
                workItemModifier.Associate(workItemId, changesetId);
                workItemModifier.SetWorkItemFixed(workItemId, changesetId);
                DefaultLogger logger = Container.Resolve<DefaultLogger>();
                logger.Error("Associated changeset (would have used username " + username + " if that was implemented (FIXME)", null);
            }
            catch (Exception e)
            {
                // We can't really raise an error here, because
                // we would fail the commit from the client side, while the changes
                // were already committed to the source control provider;
                // since we consider associating with work items nice but not essential,
                // we will log the error and ignore it.
                // In many cases this errored out
                // due to not having provided an XML template file with correct content
                // (thus it's using often unsupported default CodePlex-specific tags in web request
                // rather than properly supported plain TFS-only parts).
                // FIXME: forcing a manual template config on unsuspecting users is rather cumbersome -
                // this should be handled as automatically as possible.
                // For helpful resources, see
                // http://svnbridge.codeplex.com/wikipage?title=Work Items Integration
                // "Work Item Association doesn't work"
                //   http://svnbridge.codeplex.com/workitem/9889?ProjectName=svnbridge
                // "Work Item Associations"
                //   http://svnbridge.codeplex.com/workitem/12411?ProjectName=svnbridge
                logger.Error("Failed to associate work item with changeset", e);
            }
        }

        /// <summary>
        /// Abstraction helper - somehow knows how to gather the user name
        /// which happens to be associated with the current session.
        /// </summary>
        private string GetUsername()
        {
            return TfsUtil.GetUsername(credentials, serverUrl);
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

        public virtual void SetProperty(string activityId, string path, string propName, string propValue)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                if (!activity.Properties.ContainsKey(path))
                {
                    activity.Properties[path] = new Properties();
                }

                activity.Properties[path].Added[propName] = propValue;
            });
        }

        public virtual void RemoveProperty(string activityId, string path, string propName)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                if (!activity.Properties.ContainsKey(path))
                {
                    activity.Properties[path] = new Properties();
                }
                activity.Properties[path].Removed.Add(propName);
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
            // WARNING: this interface will (update: "might" - things are now improved...)
            // return filename items with a case-insensitive match,
            // due to querying into TFS-side APIs!
            // All users which rely on precise case-sensitive matching
            // will need to account for this.
            // Ideally we should offer a clean interface here
            // which ensures case-sensitive matching when needed.

            ItemMetaData firstItem = null;

            SVNPathStripLeadingSlash(ref path);

            if (version == 0 && path == "")
            {
                version = GetEarliestVersion(path);
            }

            if (version == LATEST_VERSION)
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
                    List<SourceItem> combinedItems = new List<SourceItem>(items);
                    foreach (SourceItem item in subFolderProperties)
                        combinedItems.Add(item);

                    items = combinedItems.ToArray();
                }
            }

            Dictionary<string, FolderMetaData> folders = new Dictionary<string, FolderMetaData>();
            Dictionary<string, ItemProperties> properties = new Dictionary<string, ItemProperties>();
            Dictionary<string, int> itemPropertyRevision = new Dictionary<string, int>();
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
                        string folderNameMangled = FilesysHelpers.GetCaseMangledName(item.Name);
                        folders[folderNameMangled] = (FolderMetaData)item;
                    }
                    if (firstItem == null)
                    {
                        firstItem = item;
                        if (item.ItemType == ItemType.File)
                        {
                            string folderName = FilesysHelpers.GetFolderPathPart(item.Name);
                            string folderNameMangled = FilesysHelpers.GetCaseMangledName(folderName);
                            folders[folderNameMangled] = new FolderMetaData();
                            folders[folderNameMangled].Items.Add(item);
                        }
                    }
                    else
                    {
                        string folderName = FilesysHelpers.GetFolderPathPart(item.Name);
                        string folderNameMangled = FilesysHelpers.GetCaseMangledName(folderName);
                        FolderMetaData folder = null;
                        if (!folders.TryGetValue(folderNameMangled, out folder))
                        {
                            // NOT FOUND?? (due to obeying a proper strict case sensitivity mode!?)
                            // Try very special algo to detect likely candidate folder.

                            // This problem has been observed with a Changeset
                            // where a whopping 50 files were correctly named
                            // yet 2 lone others (the elsewhere case-infamous resource.h sisters)
                            // had *DIFFERENT* folder case.
                            // Thus call this helper to (try to) locate the actually matching
                            // *pre-registered* folder via a case-insensitive lookup.
                            bool wantCaseSensitiveMatch = Configuration.SCMWantCaseSensitiveItemMatch; // workaround for CS0162 unreachable code
                            bool acceptingCaseInsensitiveResults = !wantCaseSensitiveMatch;

                            folder = (acceptingCaseInsensitiveResults) ?
                                CollectionHelpers.FindMatchingExistingFolderCandidate_CaseInsensitive(folders, folderNameMangled) : null;
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
                        if (maxChangeset < folderItem.Revision)
                            maxChangeset = folderItem.Revision;

                        if (maxLastModified < folderItem.LastModifiedDate)
                            maxLastModified = folderItem.LastModifiedDate;
                    }
                    // Hmm... is this syntax mismatch (ItemRevision vs. SubItemRevision) intended here?
                    if (item.ItemRevision < maxChangeset)
                        item.SubItemRevision = maxChangeset;

                    if (item.LastModifiedDate < maxLastModified)
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
        private static bool IsSuspectedPropertyStuff(string path)
        {
            return (path.Contains(Constants.PropFolder));
        }

        public bool IsPropertyFile(string path)
        {
            if (IsSuspectedPropertyStuff(path))
            { // found!? --> do precise checks.
                if (path.StartsWith(propFolderPlusSlash) || path.Contains("/" + propFolderPlusSlash))
                    return true;
            }
            return false;
        }

        public bool IsPropertyFolder(string path)
        {
            if (IsSuspectedPropertyStuff(path))
            { // found!? --> do precise checks.
                if (path == Constants.PropFolder || path.EndsWith("/" + Constants.PropFolder))
                    return true;
            }
            return false;
        }

        public bool IsPropertyFolderElement(string path)
        {
            if (IsSuspectedPropertyStuff(path))
            {
                return (
                    (path.StartsWith(propFolderPlusSlash) ||
                     path.EndsWith("/" + Constants.PropFolder) ||
                     path.Contains("/" + propFolderPlusSlash))
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
                    string folderName = FilesysHelpers.GetFolderPathPart(propertyKey).ToLowerInvariant();

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

        private static bool IsDeleted(string activityId, string path)
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
                            string path = item.Path.Replace("/" + propFolderPlusSlash, "/");
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
            string folderName = FilesysHelpers.GetFolderPathPart(item.Path);
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
                            break;
                        }
                    }
                });

                if (!folderFound)
                {
                    folderName = FilesysHelpers.GetFolderPathPart(item.Path.Substring(rootPath.Length));
                    if (!folderName.StartsWith("/"))
                        folderName = "/" + folderName;
                    MergeActivityResponseItem responseItem = new MergeActivityResponseItem(ItemType.Folder, folderName);
                    mergeResponse.Items.Add(responseItem);
                }
            }
        }

        private bool WriteFile(string activityId, string path, byte[] fileData, bool reportUpdatedFile)
        {
            bool isNewFile = true;

            bool replaced = false;
            if (IsDeleted(activityId, path))
            {
                replaced = true;
                RevertDelete(activityId, path);
            }

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

                    item = GetItems(LATEST_VERSION, existingPath, Recursion.None, true);
                } while (item == null);

                string localPath = GetLocalPath(activityId, path);
                List<LocalUpdate> updates = new List<LocalUpdate>();
                updates.Add(LocalUpdate.FromLocal(item.Id,
                                                  localPath.Substring(0, localPath.LastIndexOf('\\')),
                                                  item.Revision));

                item = GetItems(LATEST_VERSION, path.Substring(1), Recursion.None, true);
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
                    isNewFile = false;
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
                        isNewFile = false;
                    }
                    foreach (CopyAction copy in activity.CopiedItems)
                    {
                        if (copy.TargetPath == path)
                        {
                            addToMergeList = false;
                            break;
                        }
                    }
                }

                ServiceSubmitPendingRequests(activityId, pendRequests);
                string pathFile = MakeTfsPath(path);
                sourceControlService.UploadFileFromBytes(serverUrl, credentials, activityId, fileData, pathFile);

                if (addToMergeList)
                {
                    bool isUpdated = (!replaced && (!isNewFile || reportUpdatedFile));
                    if (isUpdated)
                    {
                        activity.MergeList.Add(new ActivityItem(pathFile, ItemType.File, ActivityItemAction.Updated));
                    }
                    else
                    {
                        activity.MergeList.Add(new ActivityItem(pathFile, ItemType.File, ActivityItemAction.New));
                    }
                }
            });

            return isNewFile;
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

                ProcessCopyItem(activityId, LATEST_VERSION, copy, true);
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

        private void ProcessCopyItem(string activityId, int versionFrom, CopyAction copyAction, bool forceRename)
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
                ItemMetaData item = GetItemsWithoutProperties(LATEST_VERSION, copyAction.Path, Recursion.None);
                // NOTE: this method assumes that the source item to be copied from does exist at HEAD.
                // If that is not the case, then another outer method (write file, ...)
                // should have been chosen.
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
                            break;
                        }
                    }
                }
                // Finally, check whether localPath vs. localTargetPath has a case mismatch only.
                // If so, that copy needs to be a rename, too, since on case insensitive systems
                // only one case variant can occupy the target place,
                // i.e. duplication caused by a COPY would be a problem.
                // Hmm, should we be using the ConvertCopyToRename() helper here?
                if (!copyIsRename)
                {
                    if (ItemMetaData.IsSamePathCaseInsensitive(localPath, localTargetPath))
                        copyIsRename = true;
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
            if (itemPath.StartsWith(propFolderPlusSlash))
            {
              if (itemPath == propFolderPlusSlash + Constants.FolderPropFile)
              {
                itemPath = "";
              }
              else
                itemPath = path.Substring(propFolderPlusSlash.Length);
            }
            else
            {
                itemPath = itemPath.Replace("/" + propFolderPlusSlash + Constants.FolderPropFile, "");
                itemPath = itemPath.Replace("/" + propFolderPlusSlash, "/");
            }
            return itemPath;
        }

        private static string GetPropertiesFileName(string path, ItemType itemType)
        {
            if (itemType == ItemType.Folder)
            {
                if (path == "/")
                    return "/" + propFolderPlusSlash + Constants.FolderPropFile;
                return path + "/" + propFolderPlusSlash + Constants.FolderPropFile;
            }
            if (path.LastIndexOf('/') != -1)
            {
                return
                    path.Substring(0, path.LastIndexOf('/')) + "/" + Constants.PropFolder +
                    path.Substring(path.LastIndexOf('/'));
            }
            return propFolderPlusSlash + path;
        }

        private void ProcessDeleteItem(string activityId, string path)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                string localPath = GetLocalPath(activityId, path);

                ItemMetaData item = GetItems(LATEST_VERSION, path, Recursion.None, true);
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
                item = GetItems(LATEST_VERSION, propertiesPath, Recursion.None, true);
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
                    ItemMetaData propertiesFolderItem = GetItems(LATEST_VERSION, propertiesFolder, Recursion.None, true);
                    if ((propertiesFolderItem == null) && !activity.Collections.Contains(propertiesFolder))
                    {
                        MakeCollection(activityId, propertiesFolder);
                    }

                    properties.Properties.AddRange(propertiesToAdd.Values);

                    bool reportUpdatedFile = (null != item);
                    WriteFile(activityId, propertiesPath, Helper.SerializeXml(properties), reportUpdatedFile);
                }
            });
        }

        private ItemProperties GetItemProperties(Activity activity, string path, out ItemMetaData item, out ItemType itemType)
        {
            itemType = ItemType.File;
            item = GetItems(LATEST_VERSION, path, Recursion.None);
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
                    string folderName = FilesysHelpers.GetFolderPathPart(itemProperties.Key)
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

        public virtual ItemMetaData[] GetPreviousVersionOfItems(SourceItem[] items, int changeset_Newer)
        {
            ItemMetaData[] result;

            SourceItem[] itemsPrev = QueryPreviousVersionOfSourceItems(
                items,
                changeset_Newer);

            result = itemsPrev.Select(sourceItem => (null != sourceItem) ? ConvertSourceItem(sourceItem, rootPath) : null).ToArray();

            return result;
        }

        /// <summary>
        /// Helper to figure out the previous version of a list of source items,
        /// properly symmetric within the same implementation layer!
        /// (*from* SourceItem-typed input *to* SourceItem-typed output).
        /// </summary>
        ///
        /// References:
        /// http://stackoverflow.com/questions/8946508/tfs-2010-api-get-old-name-location-of-renamed-moved-item
        /// <param name="items">List of items to be queried</param>
        /// <param name="changeset_Newer">The changeset that is newer than the result that we're supposed to determine</param>
        /// <returns>Container of items at the older changeset revision</returns>
        private SourceItem[] QueryPreviousVersionOfSourceItems(
            SourceItem[] items,
            int changeset_Newer)
        {
            SourceItem[] result;

            // Processing steps:
            // - given the this-changeset source items,
            //   figure out the corresponding maximally-authoritative representation (numeric IDs) of these items
            // - do a QueryItems() with these IDs, on the *previous* changeset

            BranchItem[] renamedItems = GetRenamedItems(items, changeset_Newer);

            var previousRevision = changeset_Newer - 1;

            // Rather than the prior hard if/else skipping of branches,
            // we'll now do handling of TFS08/multi right in the very same code flow,
            // to gain the capability of dynamically choosing (via configurable high-level bools)
            // which branch to actually add to the execution.
            // The reason for that is that I'm entirely unsure about the reasoning/differences
            // between the <=TFS08 implementation and OTOH the multi stuff
            // (and currently there's a data mismatch of members in case of a RenamedSourceItem
            // vs. the results queried here!),
            // thus debugging needs to be very easy, to finally gain sufficient insight.
            SourceItem[] resultTFS08Fallback = null;
            SourceItem[] resultMulti = null;

            // What *exactly* is the significance of this check? Rename/comment variable as needed...
            bool isAllItemsWithNullContent = (renamedItems.All(item => (null == item) || (null == item.FromItem)));
            bool needTfs08FallbackAlgo = isAllItemsWithNullContent;
            bool wantTfs08FallbackAlgo = needTfs08FallbackAlgo;

            bool wantMultiRequestMode = false;
            bool wantDebugResults = false;
            //wantDebugResults = true; // DEBUG_SITE: UNCOMMENT IF DESIRED (or simply ad-hoc toggle var in debugger)
            if (wantDebugResults)
            {
                wantTfs08FallbackAlgo = true;
                wantMultiRequestMode = true;
            }

            if (wantTfs08FallbackAlgo)
            {
                // fallback for TFS08 and earlier
                var previousSourceItemIds = items.Select(item => item.ItemId).ToArray();
                resultTFS08Fallback = metaDataRepository.QueryItems(
                    previousRevision,
                    previousSourceItemIds);
            }
            // This multi-request style is O(n) as opposed to ~ O(1), network-wise
            // (and network-side processing complexity is all that matters!),
            // thus it's prone to socket exhaustion exceptions and terrible performance.
            if (wantMultiRequestMode)
            {
                List<SourceItem> resultMulti_List = new List<SourceItem>();
                for (var i = 0; i < renamedItems.Length; i++)
                {
                    var renamedItem = renamedItems[i];
                    var previousSourceItemId = GetItemIdOfRenamedItem(renamedItem, items[i]);
                    var previousSourceItems = metaDataRepository.QueryItems(
                        previousRevision,
                        previousSourceItemId
                    );
                    // Yes, do actively append this slot even if no result
                    // (caller requires index-consistent behaviour of input vs. result storage)
                    resultMulti_List.Add(previousSourceItems.Length > 0 ? previousSourceItems[0] : null);
                }
                resultMulti = resultMulti_List.ToArray();
            }
            result = needTfs08FallbackAlgo ? resultTFS08Fallback : resultMulti;

            return result;
        }

        private BranchItem[] GetRenamedItems(SourceItem[] items, int changeset)
        {
            BranchItem[] renamedItems;

            {
                BranchItem[][] thisRevBranches;
                {
                    // FIXME: I'm totally in the dark about the reason for doing QueryBranches()/renamedItems.
                    // Possibly this is required for a different constellation. Please comment properly
                    // once it's known what this is for.
                    // FIXME_PERFORMANCE: QueryBranches() is very slow!
                    // (note that behaviour of this web service request delay seems to be linear:
                    // about 1 second per 100 items).
                    thisRevBranches = sourceControlService.QueryBranches(serverUrl,
                                                                         credentials,
                                                                         items.Select(item => CreateItemSpec(MakeTfsPath(item.RemoteName), RecursionType.None)).ToArray(),
                                                                         VersionSpec.FromChangeset(changeset));
                }
                renamedItems = items.Select((item, i) =>
                    thisRevBranches[i].FirstOrDefault(branchItem =>
                        branchItem.ToItem != null &&
                        branchItem.ToItem.RemoteChangesetId == changeset &&
                        branchItem.ToItem.RemoteName == MakeTfsPath(item.RemoteName))).ToArray();
            }

            return renamedItems;
        }

        /// <summary>
        /// Returns the ID of the item (the one *prior* to a rename/branching operation).
        /// </summary>
        /// <returns>Item ID</returns>
        static int GetItemIdOfRenamedItem(BranchItem renamedItem, SourceItem sourceItem)
        {
            // [[
            // I believe that the previous use of .FromItem was wrong: we need to use .ToItem
            // since we want to do a query with the *current* item ID, on the *previous* changeset.
            // Otherwise we will not get the correct path (would return foreign-TeamProject path
            // rather than the now-renamed one that had previously been branched into our TeamProject).
            // FIXME: *previous*SourceItemId thus most likely is now a misnomer.
            // ]]
            // NOPE, this change caused an issue around changeset 1356 in our main repo -
            // That file *was* merely renamed (not branched!), with its itemId thus changed to a new one.
            // This meant that when grabbing .ToItem.ItemId,
            // that *new* item ID then was not available at the *older* revision -->
            // null item result!!
            // (TODO: add further descriptions of evidence cases here).
            // Thus I'm afraid we'll have to revisit things
            // (in fact keep it as .FromItem, and add support code
            // to in the branching case then somehow derive the proper name).
            // And in fact renamedItem gets gathered
            // via a complex evaluation from a QueryBranches() call,
            // so perhaps that previous handling simply was wrong!?
            // (or not clever enough?)
            // UPDATE: yup, I have a hunch
            // that one may need to discern between
            // renames of items (criteria for detecting renames likely is
            //                   that .FromItem and .ToItem have *same* changeset value)
            // and
            // branches (/copies) (.ToItem will have new changeset value
            //                     which adopted a branch / copy of .FromItem - at its last changeset)
            // , so it's this difference between renames and branches
            // which probably plays a role here...
            return (renamedItem != null && renamedItem.FromItem != null) ? renamedItem.FromItem.ItemId : sourceItem.ItemId;
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
            item.Name = FilesysHelpers.StripPrefix(rootPath, sourceItem.RemoteName);

            item.Author = "unknown";
            item.LastModifiedDate = sourceItem.RemoteDate;
            item.ItemRevision = sourceItem.RemoteChangesetId;
            item.DownloadUrl = sourceItem.DownloadUrl;
            return item;
        }
    }
}
