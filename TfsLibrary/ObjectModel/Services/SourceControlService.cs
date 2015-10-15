using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows.Forms;
using System.Linq;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;

namespace CodePlex.TfsLibrary.ObjectModel
{
    public class SourceControlService : ISourceControlService
    {
        readonly IFileSystem fileSystem;
        readonly IRegistrationService registrationService;
        readonly IRepositoryWebSvcFactory webSvcFactory;
        readonly IWebTransferService webTransferService;

        public SourceControlService(IRegistrationService registrationService,
                                    IRepositoryWebSvcFactory webSvcFactory,
                                    IWebTransferService webTransferService,
                                    IFileSystem fileSystem)
        {
            this.registrationService = registrationService;
            this.webSvcFactory = webSvcFactory;
            this.webTransferService = webTransferService;
            this.fileSystem = fileSystem;
        }

        public virtual void AddWorkspaceMapping(string tfsUrl,
                                        ICredentials credentials,
                                        string workspaceName,
                                        string serverPath,
                                        string localPath,
                                        int supportedFeatures)
        {
            WrapWebException(
                delegate
                {
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        string username = TfsUtil.GetUsername(credentials, tfsUrl);
                        Workspace workspace = webSvc.QueryWorkspace(workspaceName, username);
                        workspace.AddWorkingFolder(new WorkingFolder(serverPath, Path.GetFullPath(localPath), WorkingFolderType.Map));
                        webSvc.UpdateWorkspace(workspaceName, username, workspace, supportedFeatures);
                    }
                });
        }

        public virtual int Commit(string tfsUrl,
                          ICredentials credentials,
                          string workspaceName,
                          string comment,
                          IEnumerable<string> serverItems,
                          bool deferCheckIn,
                          int checkInTicket)
        {
            return WrapWebException<int>(
                delegate
                {
                    List<string> items = new List<string>(serverItems);
                    Failure[] failures;

                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        string username = TfsUtil.GetUsername(credentials, tfsUrl);
                        CheckinNotificationInfo notifyInfo = new CheckinNotificationInfo();
                        Changeset info = new Changeset();
                        info.Comment = comment;
                        info.owner = username;

                        CheckinResult result = webSvc.CheckIn(workspaceName, username, items.ToArray(), info, notifyInfo, CheckinOptions.ValidateCheckinOwner, deferCheckIn, checkInTicket, out failures);

                        List<Failure> realFailures = FilterFailures(failures);
                        if (realFailures.Count > 0)
                            throw new TfsFailureException(TfsUtil.FailuresToMessage(realFailures));

                        return result.cset;
                    }
                });
        }

        public virtual void CreateWorkspace(string tfsUrl,
                                    ICredentials credentials,
                                    string workspaceName,
                                    string workspaceComment)
        {
            WrapWebException(
                delegate
                {
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        string username = TfsUtil.GetUsername(credentials, tfsUrl);
                        string computerName = SystemInformation.ComputerName;
                        Workspace workspace = new Workspace(workspaceName, computerName, username, workspaceComment);

                        try
                        {
                            webSvc.CreateWorkspace(workspace);
                        }
                        catch (Exception e)
                        {
                            if (e.Message.StartsWith("TF14044:"))
                                throw new NetworkAccessDeniedException(e);
                        }
                    }
                });
        }

        public virtual void DeleteWorkspace(string tfsUrl,
                                    ICredentials credentials,
                                    string workspaceName)
        {
            WrapWebException(
                delegate
                {
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        try
                        {
                            webSvc.DeleteWorkspace(workspaceName, TfsUtil.GetUsername(credentials, tfsUrl));
                        }
                        catch (Exception e)
                        {
                            if (!e.Message.StartsWith("TF14061:"))
                                throw;
                        }
                    }
                });
        }

        static List<Failure> FilterFailures(IEnumerable<Failure> failures)
        {
            List<Failure> realFailures = new List<Failure>();

            foreach (Failure failure in failures)
                if (failure.Warnings == null || failure.Warnings.Length == 0)
                    realFailures.Add(failure);

            return realFailures;
        }

        public virtual int GetLatestChangeset(string tfsUrl,
                                      ICredentials credentials)
        {
            return WrapWebException<int>(
                delegate
                {
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        try
                        {
                            return webSvc.GetRepositoryProperties().lcset;
                        }
                        catch (Exception e)
                        {
                            if (e.Message.StartsWith("TF14002:"))
                                throw new NetworkAccessDeniedException(e);
                            else
                                throw;
                        }
                    }
                });
        }


        public virtual Guid GetRepositoryId(string tfsUrl,
									  ICredentials credentials)
		{
			return WrapWebException<Guid>(
				delegate
				{
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        return webSvc.GetRepositoryProperties().id;
                    }
				});
		}

        public virtual WorkspaceInfo[] GetWorkspaces(string tfsUrl,
                                             ICredentials credentials,
                                             WorkspaceComputers computers,
                                             int permissionsFilter)
        {
            return WrapWebException<WorkspaceInfo[]>(
                delegate
                {
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        Workspace[] workspaces = webSvc.QueryWorkspaces(TfsUtil.GetUsername(credentials, tfsUrl), null, permissionsFilter);
                        List<WorkspaceInfo> results = new List<WorkspaceInfo>();

                        for (int i = 0; i < workspaces.Length; ++i)
                            if (computers == WorkspaceComputers.AllComputers || String.Compare(workspaces[i].computer, Environment.MachineName, true) == 0)
                                results.Add(new WorkspaceInfo(workspaces[i].name, workspaces[i].computer, workspaces[i].owner, workspaces[i].Comment));

                        return results.ToArray();
                    }
                });
        }

        public virtual void PendChanges(string tfsUrl,
                                ICredentials credentials,
                                string workspaceName,
                                IEnumerable<PendRequest> requests,
                                int pendChangesOptions,
                                int supportedFeatures)
        {
            WrapWebException(
                delegate
                {
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        ChangeRequest[] adds;
                        ChangeRequest[] edits;
                        ChangeRequest[] deletes;
                        ChangeRequest[] copies;
                        ChangeRequest[] renames;
                        string username = TfsUtil.GetUsername(credentials, tfsUrl);

                        TfsUtil.PendRequestsToChangeRequests(requests, out adds, out edits, out deletes, out copies, out renames);

                        PendChangesHelper(webSvc, workspaceName, username, adds, pendChangesOptions, supportedFeatures);
                        PendChangesHelper(webSvc, workspaceName, username, edits, pendChangesOptions, supportedFeatures);
                        PendChangesHelper(webSvc, workspaceName, username, deletes, pendChangesOptions, supportedFeatures);
                        PendChangesHelper(webSvc, workspaceName, username, copies, pendChangesOptions, supportedFeatures);
                        PendChangesHelper(webSvc, workspaceName, username, renames, pendChangesOptions, supportedFeatures);
                    }
                });
        }

        static void PendChangesHelper(IRepositoryWebSvc webSvc,
                                      string workspaceName,
                                      string username,
                                      ChangeRequest[] changes,
                                      int pendChangesOptions,
                                      int supportedFeatures)
        {
            WrapWebException(
                delegate
                {
                    if (changes.Length == 0)
                        return;

                    Failure[] failures;

                    webSvc.PendChanges(workspaceName, username, changes, pendChangesOptions, supportedFeatures, out failures);

                    List<Failure> realFailures = FilterFailures(failures);
                    if (realFailures.Count > 0)
                        throw new TfsFailureException(TfsUtil.FailuresToMessage(realFailures));
                });
        }

        // FIXME!!! the two QueryItems() library API variants below
        // do some ATROCIOUS LAYER VIOLATION -
        // they decide to apply a totally bogus and unhelpful Sort() -
        // such awful mangling of pristine database-side payload data
        // should only be done manually by certain *user* layers
        // which for strange reasons have the constraint of requiring the result to be sorted.
        // To add insult to injury, one QueryItems() skips Add() of null items, too.
        // Such implementation completely ignores
        // that the request-side array might have been the result of another query
        // (e.g. QueryBranches())
        // where both request and result side arrays thus better ought to fulfill
        // an array index consistency guarantee!!
        // (else the user will have to do painfully laborious post-damage processing
        // by correction-resorting things via their item IDs...).

        public virtual SourceItem[] QueryItems(string tfsUrl,
                                       ICredentials credentials,
                                       string serverPath,
                                       RecursionType recursion,
                                       VersionSpec version,
                                       DeletedState deletedState,
                                       ItemType itemType,
                                       bool sortAscending,
                                       int options)
        {
            return WrapWebException<SourceItem[]>(
                delegate
                {
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        string downloadUrlPrefix = registrationService.GetServiceInterfaceUrl(tfsUrl, credentials, "VersionControl", "Download");

                        ItemSpec spec = new ItemSpec();
                        spec.item = serverPath;
                        spec.recurse = recursion;

                        ItemSet[] itemSets = webSvc.QueryItems(null, null, new ItemSpec[1] { spec }, version, deletedState, itemType, true, options);

                        if (itemSets[0].Items.Length == 0)
                        {
                            // We call QueryHistory here so we can differentiate between failures because of no access and
                            // failures because of bad paths and/or revisions. QueryHistory will throw an exception for a
                            // bad path or bad version; if it was successful, then we ended up getting no items because
                            // we didn't have access (and thus should be throwing NetworkAccessDeniedException).

                            spec.recurse = RecursionType.None;
                            try
                            {
                                webSvc.QueryHistory(null, null, spec, version, null, version, version, 1, false, false, false, sortAscending);
                            }
                            catch  // file does not exists
                            {
                                return new SourceItem[] { };
                            }

                            throw new NetworkAccessDeniedException();
                        }

                        List<SourceItem> result = new List<SourceItem>();

                        foreach (Item item in itemSets[0].Items)
                            result.Add(SourceItem.FromRemoteItem(item.itemid, item.type, item.item, item.cs, item.len, item.date, downloadUrlPrefix + "?" + item.durl));

                        result.Sort();
                        return result.ToArray();
                    }
                });
        }

        public virtual SourceItem[] QueryItems(string tfsUrl,
                                       ICredentials credentials,
                                       int[] itemIds,
                                       int changeSet,
                                       int options)
        {
            return WrapWebException<SourceItem[]>(
                delegate
                {
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        Item[] items = webSvc.QueryItemsById(itemIds, changeSet, false, options);

                        List<SourceItem> result = new List<SourceItem>();

                        foreach (Item item in items)
                        {
                            if (item != null)
                                result.Add(SourceItem.FromRemoteItem(item.itemid, item.type, item.item, item.cs, item.len, item.date, null));
                        }

                        result.Sort();
                        return result.ToArray();
                    }
                });
        }

        public virtual BranchItem[][] QueryBranches(string tfsUrl,
                                                    ICredentials credentials,
                                                    ItemSpec[] items,
                                                    VersionSpec version)
        {
            return WrapWebException(
                delegate
                {
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        var branchQueries = webSvc.QueryBranches(null, null, items, version);
                        return branchQueries.Select(branchQuery => branchQuery.Select(branch => new BranchItem
                        {
                            FromItem = branch.BranchFromItem != null ? SourceItem.FromRemoteItem(branch.BranchFromItem) : null,
                            ToItem = branch.BranchToItem != null ? SourceItem.FromRemoteItem(branch.BranchToItem) : null
                        }).ToArray()).ToArray();
                    }
                });
        }

        public virtual LogItem QueryLog(string tfsUrl,
                                ICredentials credentials,
                                string serverPath,
                                VersionSpec versionFrom,
                                VersionSpec versionTo,
                                RecursionType recursionType,
                                int maxCount,
                                bool sortAscending)
        {
            return WrapWebException<LogItem>(
                delegate
                {
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        ItemSpec itemSpec = new ItemSpec();
                        itemSpec.item = serverPath;
                        itemSpec.recurse = recursionType;
                        Changeset[] changes = webSvc.QueryHistory(null, null, itemSpec, VersionSpec.Latest, null, versionFrom,
                                                                  versionTo, maxCount, true, false, false, sortAscending);

                        List<SourceItemHistory> history = new List<SourceItemHistory>();

                        foreach (Changeset changeset in changes)
                        {
                            SourceItemHistory sourceItemHistory = new SourceItemHistory(changeset.Changes[0].Item.cs, changeset.cmtr,
                                                                                        changeset.date, changeset.Comment);

                            foreach (Change change in changeset.Changes)
                                sourceItemHistory.Changes.Add(new SourceItemChange(SourceItem.FromRemoteItem(change.Item.itemid,
                                                                                                             change.Item.type,
                                                                                                             change.Item.item,
                                                                                                             change.Item.cs,
                                                                                                             change.Item.len,
                                                                                                             change.Item.date,
                                                                                                             null),
                                                                                   change.type));

                            history.Add(sourceItemHistory);
                        }

                        return new LogItem(null, serverPath, history.ToArray());
                    }
                });
        }

        public virtual void UndoPendingChanges(string tfsUrl,
                                       ICredentials credentials,
                                       string workspaceName,
                                       IEnumerable<string> serverItems)
        {
            WrapWebException(
                delegate
                {
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        string username = TfsUtil.GetUsername(credentials, tfsUrl);
                        Failure[] failures;

                        List<ItemSpec> items = new List<ItemSpec>();
                        foreach (string serverItem in serverItems)
                        {
                            ItemSpec item = new ItemSpec();
                            item.item = serverItem;
                            items.Add(item);
                        }

                        webSvc.UndoPendingChanges(workspaceName, username, items.ToArray(), out failures);

                        List<Failure> realFailures = FilterFailures(failures);
                        if (realFailures.Count > 0)
                            throw new TfsFailureException(TfsUtil.FailuresToMessage(realFailures));
                    }
                });
        }

        public virtual void UpdateLocalVersions(string tfsUrl,
                                        ICredentials credentials,
                                        string workspaceName,
                                        IEnumerable<LocalUpdate> updates)
        {
            WrapWebException(
                delegate
                {
                    using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                    {
                        webSvc.UpdateLocalVersion(workspaceName,
                                                  TfsUtil.GetUsername(credentials, tfsUrl),
                                                  TfsUtil.LocalUpdatesToLocalVersionUpdates(updates));
                    }
                });
        }

        public virtual void UploadFile(string tfsUrl,
                               ICredentials credentials,
                               string workspaceName,
                               string localPath,
                               string serverPath)
        {
            UploadFileFromBytes(tfsUrl, credentials, workspaceName, fileSystem.ReadAllBytes(localPath), serverPath);
        }

        public virtual void UploadFileFromBytes(string tfsUrl,
                                        ICredentials credentials,
                                        string workspaceName,
                                        byte[] localContents,
                                        string serverPath)
        {
            WrapWebException(
                delegate
                {
                    string uploadUrl = registrationService.GetServiceInterfaceUrl(tfsUrl, credentials, "VersionControl", "Upload");
                    string username = TfsUtil.GetUsername(credentials, tfsUrl);
                    int fileSize = localContents.Length;

                    WebTransferFormData formData = webTransferService.CreateFormPostData();
                    formData.Add("item", serverPath);
                    formData.Add("wsname", workspaceName);
                    formData.Add("wsowner", username);
                    formData.Add("filelength", fileSize.ToString());
                    formData.Add("hash", Convert.ToBase64String(fileSystem.GetFileHash(localContents)));
                    formData.Add("range", string.Format("bytes=0-{0}/{1}", fileSize - 1, fileSize));
                    formData.AddFile("item", localContents);

                    webTransferService.PostForm(uploadUrl, credentials, formData);
                });
        }

        public virtual void UploadFileFromStream(string tfsUrl,
                                         ICredentials credentials,
                                         string workspaceName,
                                         Stream localContents,
                                         string serverPath)
        {
            byte[] bytes = new byte[localContents.Length];
            localContents.Read(bytes, 0, bytes.Length);
            UploadFileFromBytes(tfsUrl, credentials, workspaceName, bytes, serverPath);
        }

        public Repository CreateProxy(string tfsUrl, ICredentials credentials)
        {
            return (Repository)webSvcFactory.Create(tfsUrl, credentials);
        }

        static T WrapWebException<T>(WrapWebExceptionDelegate<T> function)
        {
            try
            {
                return function();
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;

                if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new NetworkAccessDeniedException(ex);

                throw;
            }
        }

        static void WrapWebException(WrapWebExceptionDelegate function)
        {
            try
            {
                function();
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;

                if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new NetworkAccessDeniedException(ex);

                throw;
            }
        }

        delegate T WrapWebExceptionDelegate<T>();

        delegate void WrapWebExceptionDelegate();
    }
}
