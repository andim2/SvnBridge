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
                              out Failure[] failures);

        Workspace CreateWorkspace(Workspace workspace);

        void DeleteWorkspace(string workspaceName,
                             string ownerName);

        GetOperation[][] Get(string workspaceName,
                             string ownerName,
                             GetRequest[] requests,
                             bool force,
                             bool noGet);

        RepositoryProperties GetRepositoryProperties();

        GetOperation[] PendChanges(string workspaceName,
                                   string ownerName,
                                   ChangeRequest[] changes,
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
                                 bool slotMode);

        ItemSet[] QueryItems(string workspaceName,
                             string workspaceOwner,
                             ItemSpec[] items,
                             VersionSpec version,
                             DeletedState deletedState,
                             ItemType itemType,
                             bool generateDownloadUrls);

        Item[] QueryItemsById(int[] itemIds,
                              int changeSet,
                              bool generateDownloadUrls);

        Workspace QueryWorkspace(string workspaceName,
                                 string ownerName);

        Workspace[] QueryWorkspaces(string ownerName,
                                    string computer);

        GetOperation[] UndoPendingChanges(string workspaceName,
                                          string ownerName,
                                          ItemSpec[] items,
                                          out Failure[] failures);

        void UpdateLocalVersion(string workspaceName,
                                string ownerName,
                                LocalVersionUpdate[] updates);

        Workspace UpdateWorkspace(string oldWorkspaceName,
                                  string ownerName,
                                  Workspace newWorkspace);

        BranchRelative[][] QueryBranches(string workspaceName, 
                                         string workspaceOwner, 
                                         ItemSpec[] items, 
                                         VersionSpec version);

        #region Extracted from web service

        //bool UseDefaultCredentials { get;set;}

        //void AddConflict(string workspaceName, string ownerName, ConflictType conflictType, int itemId, int versionFrom, int pendingChangeId, string sourceLocalItem, string targetLocalItem, int reason);
        //string CheckAuthentication();
        //CheckinResult CheckIn(string workspaceName, string ownerName, string[] serverItems, Changeset info, CheckinNotificationInfo checkinNotificationInfo, CheckinOptions checkinOptions, out Failure[] failures);
        //Failure[] CheckPendingChanges(string workspaceName, string ownerName, string[] serverItems);
        //void CreateAnnotation(string AnnotationName, string AnnotatedItem, int Version, string AnnotationValue, string Comment, bool Overwrite);
        //void CreateCheckinNoteDefinition(string associatedServerItem, CheckinNoteFieldDefinition[] checkinNoteFields);
        //void DeleteAnnotation(string AnnotationName, string AnnotatedItem, int Version, string AnnotationValue);
        //LabelResult[] DeleteLabel(string labelName, string labelScope);
        //void DeleteShelveset(string shelvesetName, string ownerName);
        //LabelResult[] LabelItem(string workspaceName, string workspaceOwner, VersionControlLabel label, LabelItemSpec[] labelSpecs, LabelChildOption children, out Failure[] failures);
        //GetOperation[] Merge(string workspaceName, string workspaceOwner, ItemSpec source, ItemSpec target, VersionSpec from, VersionSpec to, MergeOptions options, LockLevel lockLevel, out Failure[] failures, out Conflict[] conflicts);
        //Annotation[] QueryAnnotation(string annotationName, string annotatedItem, int version);
        //Changeset QueryChangeset(int ChangeSetID, bool includeChanges, bool generateDownloadUrls);
        //CheckinNoteFieldDefinition[] QueryCheckinNoteDefinition(string[] associatedServerItem);
        //string[] QueryCheckinNoteFieldNames();
        //Conflict[] QueryConflicts(string workspaceName, string ownerName, ItemSpec[] items);
        //string[] QueryEffectiveGlobalPermissions(string identityName);
        //string[] QueryEffectiveItemPermissions(string workspaceName, string workspaceOwner, string item, string identityName);
        //FileType[] QueryFileTypes();
        //GlobalSecurity QueryGlobalPermissions(string[] identityNames);
        //ExtendedItem[][] QueryItemsExtended(string workspaceName, string workspaceOwner, ItemSpec[] items, DeletedState deletedState, ItemType itemType);
        //ItemSecurity[] QueryItemPermissions(string workspaceName, string workspaceOwner, ItemSpec[] itemSpecs, string[] identityNames, out Failure[] failures);
        //VersionControlLabel[] QueryLabels(string workspaceName, string workspaceOwner, string labelName, string labelScope, string owner, string filterItem, VersionSpec versionFilterItem, bool includeItems, bool generateDownloadUrls);
        //MergeCandidate[] QueryMergeCandidates(string workspaceName, string workspaceOwner, ItemSpec source, ItemSpec target);
        //ChangesetMerge[] QueryMerges(string workspaceName, string workspaceOwner, ItemSpec source, VersionSpec versionSource, ItemSpec target, VersionSpec versionTarget, VersionSpec versionFrom, VersionSpec versionTo, int maxChangesets, out Changeset[] changesets);
        //PendingSet[] QueryPendingSets(string localWorkspaceName, string localWorkspaceOwner, string queryWorkspaceName, string ownerName, ItemSpec[] itemSpecs, bool generateDownloadUrls, out Failure[] failures);
        //PendingSet[] QueryShelvedChanges(string localWorkspaceName, string localWorkspaceOwner, string shelvesetName, string ownerName, ItemSpec[] itemSpecs, bool generateDownloadUrls, out Failure[] failures);
        //Shelveset[] QueryShelvesets(string shelvesetName, string ownerName);
        //Workspace[] QueryWorkspaces(string ownerName, string computer);
        //void RefreshIdentityDisplayName();
        //void RemoveLocalConflict(string workspaceName, string ownerName, int conflictId);
        //GetOperation[] Resolve(string workspaceName, string ownerName, int conflictId, Resolution resolution, string newPath, int encoding, LockLevel lockLevel, out GetOperation[] undoOperations, out Conflict[] resolvedConflicts);
        //void SetFileTypes(FileType[] fileTypes);
        //Failure[] Shelve(string workspaceName, string workspaceOwner, string[] serverItems, Shelveset shelveset, bool replace);
        //LabelResult[] UnlabelItem(string workspaceName, string workspaceOwner, string labelName, string labelScope, ItemSpec[] items, VersionSpec version, out Failure[] failures);
        //Shelveset Unshelve(string shelvesetName, string shelvesetOwner, string workspaceName, string workspaceOwner, ItemSpec[] items, out Failure[] failures, out GetOperation[] getOperations);
        //void UpdateChangeset(int changeset, string comment, CheckinNote checkinNote);
        //void UpdateCheckinNoteFieldName(string path, string existingFieldName, string newFieldName);
        //PermissionChange[] UpdateGlobalSecurity(PermissionChange[] changes, out Failure[] failures);
        //SecurityChange[] UpdateItemSecurity(string workspaceName, string workspaceOwner, SecurityChange[] changes, out Failure[] failures);
        //void UpdatePendingState(string workspaceName, string workspaceOwner, PendingState[] updates);
        //PendingChange[] QueryPendingChangesById(int[] pendingChangeIds, bool generateDownloadUrls);
        //void CreateTeamProjectFolder(TeamProjectFolderOptions teamProjectOptions);

        #endregion
    }
}