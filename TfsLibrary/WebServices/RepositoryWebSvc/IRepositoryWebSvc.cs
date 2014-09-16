namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public interface IRepositoryWebSvc
    {
        CheckinResult CheckIn(string workspaceName,
                              string ownerName,
                              string[] serverItems,
                              Changeset info,
                              CheckinNotificationInfo checkinNotificationInfo,
                              CheckinOptions checkinOptions,
                              bool deferCheckIn,
                              int checkInTicket,
                              out Failure[] failures);

        Workspace CreateWorkspace(Workspace workspace);

        void DeleteWorkspace(string workspaceName,
                             string ownerName);

        GetOperation[][] Get(string workspaceName,
                             string ownerName,
                             GetRequest[] requests,
                             bool force,
                             bool noGet,
                             int maxResults,
                             int options);

        RepositoryProperties GetRepositoryProperties();

        GetOperation[] PendChanges(string workspaceName,
                                   string ownerName,
                                   ChangeRequest[] changes,
                                   int pendChangesOptions,
                                   int supportedFeatures, 
                                   out Failure[] failures);

        Changeset[] QueryHistory(string workspaceName,
                                 string workspaceOwner,
                                 ItemSpec itemSpec,
                                 VersionSpec versionItem,
                                 string user,
                                 VersionSpec versionFrom,
                                 VersionSpec versionTo,
                                 int maxCount,
                                 bool includeFiles,
                                 bool generateDownloadUrls,
                                 bool slotMode,
                                 bool sortAscending);

        ItemSet[] QueryItems(string workspaceName,
                             string workspaceOwner,
                             ItemSpec[] items,
                             VersionSpec version,
                             DeletedState deletedState,
                             ItemType itemType,
                             bool generateDownloadUrls,
                             int options);

        Item[] QueryItemsById(int[] itemIds,
                              int changeSet,
                              bool generateDownloadUrls,
                              int options);

        Workspace QueryWorkspace(string workspaceName,
                                 string ownerName);

        Workspace[] QueryWorkspaces(string ownerName,
                                    string computer,
                                    int permissionsFilter);

        GetOperation[] UndoPendingChanges(string workspaceName,
                                          string ownerName,
                                          ItemSpec[] items,
                                          out Failure[] failures);

        void UpdateLocalVersion(string workspaceName,
                                string ownerName,
                                LocalVersionUpdate[] updates);

        Workspace UpdateWorkspace(string oldWorkspaceName,
                                  string ownerName,
                                  Workspace newWorkspace,
                                  int supportedFeatures);

        BranchRelative[][] QueryBranches(string workspaceName, 
                                         string workspaceOwner, 
                                         ItemSpec[] items, 
                                         VersionSpec version);
    }
}