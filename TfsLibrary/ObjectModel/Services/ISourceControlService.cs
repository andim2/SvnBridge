using System.Collections.Generic;
using System.IO;
using System.Net;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ObjectModel
{
	using System;

	public interface ISourceControlService
    {
        void AddWorkspaceMapping(string tfsUrl,
                                 ICredentials credentials,
                                 string workspaceName,
                                 string serverPath,
                                 string localPath,
                                 int supportedFeatures);

        int Commit(string tfsUrl,
                   ICredentials credentials,
                   string workspaceName,
                   string comment,
                   IEnumerable<string> serverItems,
                   bool deferCheckIn,
                   int checkInTicket);

        void CreateWorkspace(string tfsUrl,
                             ICredentials credentials,
                             string workspaceName,
                             string workspaceComment);

        void DeleteWorkspace(string tfsUrl,
                             ICredentials credentials,
                             string workspaceName);


		Guid GetRepositoryId(string tfsUrl,
							   ICredentials credentials);


        int GetLatestChangeset(string tfsUrl,
                               ICredentials credentials);

        WorkspaceInfo[] GetWorkspaces(string tfsUrl,
                                      ICredentials credentials,
                                      WorkspaceComputers computers,
                                      int permissionsFilter);

        void PendChanges(string tfsUrl,
                         ICredentials credentials,
                         string workspaceName,
                         IEnumerable<PendRequest> requests,
                         int pendChangesOptions,
                         int supportedFeatures);

        SourceItem[] QueryItems(string tfsUrl,
                                ICredentials credentials,
                                string serverPath,
                                RecursionType recursion,
                                VersionSpec version,
                                DeletedState deletedState,
                                ItemType itemType,
                                bool sortAscending,
                                int options);

        SourceItem[] QueryItems(string tfsUrl,
                                ICredentials credentials,
                                int[] itemIds,
                                int changeSet,
                                int options);

        LogItem QueryLog(string tfsUrl,
                         ICredentials credentials,
                         string serverPath,
                         VersionSpec versionFrom,
                         VersionSpec versionTo,
                         RecursionType recursionType,
                         int maxCount,
                         bool sortAscending);

        void UndoPendingChanges(string tfsUrl,
                                ICredentials credentials,
                                string workspaceName,
                                IEnumerable<string> serverItems);

        void UpdateLocalVersions(string tfsUrl,
                                 ICredentials credentials,
                                 string workspaceName,
                                 IEnumerable<LocalUpdate> updates);

        void UploadFile(string tfsUrl,
                        ICredentials credentials,
                        string workspaceName,
                        string localPath,
                        string serverPath);

        void UploadFileFromBytes(string tfsUrl,
                                 ICredentials credentials,
                                 string workspaceName,
                                 byte[] localContents,
                                 string serverPath);

        void UploadFileFromStream(string tfsUrl,
                                  ICredentials credentials,
                                  string workspaceName,
                                  Stream localContents,
                                  string serverPath);
    }
}