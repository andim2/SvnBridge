using System.Net.Sockets;
using CodePlex.TfsLibrary;
using SvnBridge.Net;

namespace SvnBridge.SourceControl
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text.RegularExpressions;
    using CodePlex.TfsLibrary.ObjectModel;
    using CodePlex.TfsLibrary.RepositoryWebSvc;
    using Dto;
    using Exceptions;
    using Infrastructure;
    using Interfaces;
    using Protocol;
    using Proxies;
    using Utility;
    using SvnBridge.Cache;
    using System.Web.Services.Protocols;
    using System.Linq;

    [Interceptor(typeof(TracingInterceptor))]
    [Interceptor(typeof(RetryOnExceptionsInterceptor<SocketException>))]
    public class TFSSourceControlProvider : MarshalByRefObject
    {
        private static readonly Regex s_associatedWorkItems = new Regex(@"(?:(?:(?:fixe?|close|resolve)(?:s|d)?)|(?:Work ?Items?))(?: |:|: )(#?\d+(?:, ?#?\d+)*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);
        private const char c_workItemChar = '#';

        private readonly string rootPath;
        private readonly string serverUrl;
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
            if (Configuration.CacheEnabled)
            {
                this.metaDataRepository = new MetaDataRepositoryCache(
                    this.sourceControlService,
                    this.credentials,
                    Container.Resolve<MemoryBasedPersistentCache>(),
                    this.serverUrl,
                    this.rootPath);
            }
            else
            {
                this.metaDataRepository = new MetaDataRepositoryNoCache(
                    this.sourceControlService,
                    this.credentials,
                    this.serverUrl,
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
            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }

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
                foreach (ItemMetaData item in new List<ItemMetaData>(root.Items))
                {
                    string name = item.Name;
                    if (name.StartsWith("/") == false)
                        name = "/" + name;
                    if (name.Equals(targetPath, StringComparison.InvariantCultureIgnoreCase) == false)
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
            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }

            string serverPath = Helper.CombinePath(rootPath, path);
            RecursionType recursionType = RecursionType.None;
            switch (recursion)
            {
                case Recursion.OneLevel:
                    recursionType = RecursionType.None;
                    break;
                case Recursion.Full:
                    recursionType = RecursionType.Full;
                    break;
            }

            VersionSpec itemVersionSpec = VersionSpec.Latest;
            if (itemVersion != -1)
                itemVersionSpec = VersionSpec.FromChangeset(itemVersion);

            LogItem logItem = GetLogItem(serverPath, itemVersionSpec, versionFrom, versionTo, recursionType, maxCount, sortAscending);

            foreach (SourceItemHistory history in logItem.History)
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
                    ItemMetaData[] oldItems = GetPreviousVersionOfItems(renamedItems.ToArray(), history.ChangeSetID);
                    var oldItemsById = new Dictionary<int, ItemMetaData>();
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

                    foreach (var rename in renamesWithNoPreviousVersion)
                        history.Changes.Remove(rename);

                    history.Changes.RemoveAll(item => item.ChangeType == ChangeType.None);
                    history.Changes.RemoveAll(item => item.ChangeType == ChangeType.Delete &&
                                              oldItems.Any(oldItem => oldItem != null && oldItem.Id == item.Item.ItemId));
                }
                if (branchedItems.Count > 0)
                {
                    List<ItemSpec> items = new List<ItemSpec>();
                    foreach (SourceItem item in branchedItems)
                    {
                        ItemSpec spec = new ItemSpec();
                        spec.item = Helper.CombinePath(rootPath, item.RemoteName);
                        items.Add(spec);
                    }

                    ChangesetVersionSpec branchChangeset = new ChangesetVersionSpec();
                    branchChangeset.cs = history.ChangeSetID;
                    BranchRelative[][] branches = sourceControlService.QueryBranches(serverUrl, credentials, null, items.ToArray(), branchChangeset);

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

            return logItem;
        }

        private List<SourceItemHistory> ConvertChangesetsToSourceItemHistory(Changeset[] changes)
        {
            List<SourceItemHistory> history = new List<SourceItemHistory>();

            foreach (Changeset changeset in changes)
            {
                SourceItemHistory sourceItemHistory = new SourceItemHistory(changeset.Changes[0].Item.cs, changeset.cmtr, changeset.date, changeset.Comment);
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

        private LogItem GetLogItem(string serverPath, VersionSpec itemVersion, int versionFrom, int versionTo, RecursionType recursionType, int maxCount, bool sortAscending)
        {
            const int QUERY_LIMIT = 256;

            ItemSpec itemSpec = CreateItemSpec(serverPath, recursionType);
            Changeset[] changes;
            List<SourceItemHistory> histories;
            try
            {
                changes = sourceControlService.QueryHistory(serverUrl, credentials,
                    null, null,
                    itemSpec, itemVersion,
                    null,
                    VersionSpec.FromChangeset(versionFrom), VersionSpec.FromChangeset(versionTo),
                    maxCount,
                    true, false, false,
                    sortAscending);
            }
            catch (SoapException ex)
            {
                if ((recursionType == RecursionType.Full) && (ex.Message.EndsWith(" does not exist at the specified version.")))
                {
                    // Workaround for bug in TFS2008sp1
                    int latestVersion = GetLatestVersion();
                    LogItem log = GetLogItem(serverPath, itemVersion, 1, latestVersion, RecursionType.None, 2, sortAscending /* is this the value to pass to have this workaround still work properly? */);
                    if (log.History[0].Changes[0].ChangeType == ChangeType.Delete && log.History.Length == 2)
                        latestVersion = log.History[1].ChangeSetID;

                    if (versionTo == latestVersion)
                    {
                        // in this case, there are only 2 revisions in TFS
                        // the first being the initial checkin, and the second
                        // being the deletion, there is no need to query further
                        histories = new List<SourceItemHistory>(log.History);
                    }
                    else
                    {
                        LogItem result = GetLogItem(log.History[0].Changes[0].Item.RemoteName, VersionSpec.FromChangeset(latestVersion), 1, latestVersion, RecursionType.Full, int.MaxValue, sortAscending);
                        histories = new List<SourceItemHistory>(result.History);
                    }

                    while ((histories.Count > 0) && (histories[0].ChangeSetID > versionTo))
                    {
                        histories.RemoveAt(0);
                    }
                    while (histories.Count > maxCount)
                    {
                        histories.RemoveAt(histories.Count - 1);
                    }
                    return new LogItem(null, serverPath, histories.ToArray());
                }
                else
                    throw;
            }
            histories = ConvertChangesetsToSourceItemHistory(changes);

            // TFS QueryHistory API won't return more than 256 items,
            // so need to call multiple times if more requested
            if (maxCount > QUERY_LIMIT)
            {
                int logItemsCount = histories.Count;
                List<SourceItemHistory> temp = histories;
                while (logItemsCount == QUERY_LIMIT)
                {
                    int earliestVersionFound = temp[QUERY_LIMIT - 1].ChangeSetID - 1;
                    if (earliestVersionFound == versionFrom)
                        break;

                    changes = sourceControlService.QueryHistory(serverUrl, credentials,
                        null, null,
                        itemSpec, itemVersion,
                        null,
                        VersionSpec.FromChangeset(versionFrom), VersionSpec.FromChangeset(earliestVersionFound),
                        maxCount,
                        true, false, false,
                        sortAscending);
                    temp = ConvertChangesetsToSourceItemHistory(changes);
                    histories.AddRange(temp);
                    logItemsCount = temp.Count;
                }
            }
            return new LogItem(null, serverPath, histories.ToArray());
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
            ItemMetaData item = GetItems(version, path, Recursion.None, true);
            return (item != null);
        }

        public virtual bool ItemExists(int itemId, int version)
        {
            if (itemId == 0)
                throw new ArgumentException("item id cannot be zero", "itemId");
            var items = metaDataRepository.QueryItems(version, itemId, Recursion.None);
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
                    new ActivityItem(Helper.CombinePath(rootPath, path), ItemType.Folder, ActivityItemAction.New));
                activity.Collections.Add(path);
            });

        }

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
                        commitServerList.Add(Helper.CombinePath(rootPath, path));
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
                ItemSpec itemSpec = CreateItemSpec(rootPath, RecursionType.Full);
                Changeset[] changes = sourceControlService.QueryHistory(serverUrl, credentials,
                    null, null,
                    itemSpec, VersionSpec.Latest,
                    null,
                    VersionSpec.First, VersionSpec.FromDate(date),
                    1,
                    true, false, false, false);

                // If no results then date is before project existed
                if (changes.Length == 0)
                    return 0;

                return changes[0].cset;
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

        private ItemMetaData GetItems(int version, string path, Recursion recursion, bool returnPropertyFiles)
        {
            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }

            if (version == 0 && path == "")
            {
                version = GetEarliestVersion(path);
            }

            if (version == -1)
            {
                version = GetLatestVersion();
            }

            string propertiesForFile = GetPropertiesFileName(path, ItemType.File);
            string propertiesForFolder = GetPropertiesFileName(path, ItemType.Folder);
            string propertiesForFolderItems = path + "/" + Constants.PropFolder;
            List<string> itemPaths = new List<string>();
            itemPaths.Add(path);

            SourceItem[] items = null;
            if (returnPropertyFiles || recursion == Recursion.Full)
            {
                items = metaDataRepository.QueryItems(version, path, recursion);
            }
            else if (recursion == Recursion.None)
            {
                if (propertiesForFile.Length + rootPath.Length <= 259)
                    itemPaths.Add(propertiesForFile);

                if (propertiesForFolder.Length + rootPath.Length <= 259)
                    itemPaths.Add(propertiesForFolder);

                items = metaDataRepository.QueryItems(version, itemPaths.ToArray(), recursion);
            }
            else if (recursion == Recursion.OneLevel)
            {
                if (propertiesForFile.Length + rootPath.Length <= 259)
                    itemPaths.Add(propertiesForFile);

                if (propertiesForFolderItems.Length + rootPath.Length <= 259)
                    itemPaths.Add(propertiesForFolderItems);

                items = metaDataRepository.QueryItems(version, itemPaths.ToArray(), recursion);
                if (items.Length > 0 && items[0].ItemType == ItemType.Folder)
                {
                    List<string> propertiesForSubFolders = new List<string>();
                    foreach (SourceItem item in items)
                    {
                        if (item.ItemType == ItemType.Folder && !IsPropertyFolder(item.RemoteName))
                        {
                            propertiesForFolder = GetPropertiesFileName(item.RemoteName, ItemType.Folder);
                            if (propertiesForFolder.Length + rootPath.Length <= 259)
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
                    if (item.ItemType == ItemType.Folder)
                    {
                        folders[item.Name.ToLower()] = (FolderMetaData)item;
                    }
                    if (firstItem == null)
                    {
                        firstItem = item;
                        if (item.ItemType == ItemType.File)
                        {
                            string folderName = GetFolderName(item.Name);
                            folders[folderName.ToLower()] = new FolderMetaData();
                            folders[folderName.ToLower()].Items.Add(item);
                        }
                    }
                    else
                    {
                        string folderName = GetFolderName(item.Name);
                        folders[folderName.ToLower()].Items.Add(item);
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

        public bool IsPropertyFile(string name)
        {
            if (name.StartsWith(Constants.PropFolder + "/") || name.Contains("/" + Constants.PropFolder + "/"))
                return true;
            else
                return false;
        }

        public bool IsPropertyFolder(string name)
        {
            if (name == Constants.PropFolder || name.EndsWith("/" + Constants.PropFolder))
                return true;
            else
                return false;
        }

        private static void UpdateItemRevisionsBasedOnPropertyItemRevisions(IDictionary<string, FolderMetaData> folders, IEnumerable<KeyValuePair<string, int>> itemPropertyRevision)
        {
            foreach (KeyValuePair<string, int> propertyRevision in itemPropertyRevision)
            {
                string propertyKey = propertyRevision.Key;

                if (folders.ContainsKey(propertyKey.ToLower()))
                {
                    ItemMetaData item = folders[propertyKey.ToLower()];
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
                string serverItemPath = Helper.CombinePath(rootPath, path);
                ServiceUndoPendingRequests(activityId, new string[] { serverItemPath });
                activity.DeletedItems.Remove(path);
                for (int j = activity.MergeList.Count - 1; j >= 0; j--)
                {
                    if (activity.MergeList[j].Action == ActivityItemAction.Deleted
                        && activity.MergeList[j].Path == serverItemPath)
                    {
                        activity.MergeList.RemoveAt(j);
                    }
                }
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
                string pathFile = Helper.CombinePath(rootPath, path);
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

        private void ConvertCopyToRename(string activityId, CopyAction copy)
        {
            ActivityRepository.Use(activityId, delegate(Activity activity)
            {
                string pathTargetFull = Helper.CombinePath(rootPath, copy.TargetPath);
                ServiceUndoPendingRequests(
                                                    activityId,
                                                    new string[] { pathTargetFull });
                for (int i = activity.MergeList.Count - 1; i >= 0; i--)
                {
                    if (activity.MergeList[i].Path == pathTargetFull)
                    {
                        activity.MergeList.RemoveAt(i);
                    }
                }

                ProcessCopyItem(activityId, copy, true);
            });
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
                    activity.MergeList.Add(new ActivityItem(Helper.CombinePath(rootPath, copyAction.Path), item.ItemType, ActivityItemAction.RenameDelete));
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

                                    string pathDeletedFull = Helper.CombinePath(rootPath, activity.DeletedItems[i]);
                                    ServiceUndoPendingRequests(
                                                                            activityId,
                                                                            new string[] { pathDeletedFull });
                                    for (int j = activity.MergeList.Count - 1; j >= 0; j--)
                                    {
                                        if (activity.MergeList[j].Path == pathDeletedFull)
                                        {
                                            activity.MergeList.RemoveAt(j);
                                        }
                                    }

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

                            string pathDeletedFull = Helper.CombinePath(rootPath, activity.DeletedItems[i]);
                            ServiceUndoPendingRequests(
                                                                    activityId,
                                                                    new string[] { pathDeletedFull });
                            for (int j = activity.MergeList.Count - 1; j >= 0; j--)
                            {
                                if (activity.MergeList[j].Path == pathDeletedFull)
                                {
                                    activity.MergeList.RemoveAt(j);
                                }
                            }

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
                string pathCopyTarget = Helper.CombinePath(rootPath, copyAction.TargetPath);
                if (copyAction.Rename)
                {
                    activity.MergeList.Add(
                        new ActivityItem(pathCopyTarget, item.ItemType, ActivityItemAction.New));
                }
                else
                {
                    activity.MergeList.Add(
                        new ActivityItem(pathCopyTarget, item.ItemType, ActivityItemAction.Branch,
                            Helper.CombinePath(rootPath, copyAction.Path)));
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

                activity.MergeList.Add(new ActivityItem(Helper.CombinePath(rootPath, path), item.ItemType, ActivityItemAction.Deleted));

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
            spec.item = Helper.CombinePath(rootPath, path);
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
            var branchQueries = sourceControlService.QueryBranches(serverUrl, 
                                                                   credentials,
                                                                   items.Select(item => CreateItemSpec(rootPath + item.RemoteName, RecursionType.None)).ToArray(), 
                                                                   VersionSpec.FromChangeset(changeset));
            var renamedItems = items.Select((item, i) =>
                branchQueries[i].FirstOrDefault(branchItem => 
                    branchItem.ToItem != null && 
                    branchItem.ToItem.RemoteChangesetId == changeset && 
                    branchItem.ToItem.RemoteName == rootPath + item.RemoteName)).ToList();
            
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

        static ItemSpec CreateItemSpec(string item, RecursionType recurse)
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
