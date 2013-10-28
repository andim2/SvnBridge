using System.Collections.Generic;
using System.IO;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        protected TfsWorkspace SetupWorkspace(string localPath,
                                              bool includePendingEdits)
        {
            TfsFolderInfo folderInfo = tfsState.GetFolderInfo(localPath);
            string tfsUrl = folderInfo.TfsUrl;
            string serverPath = folderInfo.ServerPath;
            TfsWorkspace workspace = null;

            ICredentials credentials = GetCredentials(tfsUrl);

            while (true)
            {
                try
                {
                    workspace = workspaceManager.CreateWorkspace(tfsUrl, credentials);
                    sourceControlService.AddWorkspaceMapping(tfsUrl, credentials, workspace.Name, serverPath, localPath, 0);

                    List<LocalUpdate> localVersions = new List<LocalUpdate>();
                    List<PendRequest> pendRequests = new List<PendRequest>();

                    SetupWorkspace_Helper(localPath, localVersions, pendRequests);

                    if (localVersions.Count > 0)
                        sourceControlService.UpdateLocalVersions(tfsUrl, credentials, workspace.Name, localVersions);
                    if (includePendingEdits && pendRequests.Count > 0)
                        sourceControlService.PendChanges(tfsUrl, credentials, workspace.Name, pendRequests, 0, 0);

                    workspaces.Add(new WorkspaceMetadata(tfsUrl, credentials, serverPath, workspace.Name));

                    return workspace;
                }
                catch (NetworkAccessDeniedException)
                {
                    if (workspace != null)
                    {
                        workspace.Dispose();
                        workspace = null;
                    }

                    if (credentialsCallback == null)
                        throw;

                    credentials = GetCredentials(tfsUrl, true);

                    if (credentials == null)
                        throw;
                }
            }
        }

        void SetupWorkspace_Helper(string localPath,
                                   List<LocalUpdate> localUpdates,
                                   List<PendRequest> pendRequests)
        {
            // Make sure root gets added

            if (localUpdates.Count == 0)
            {
                SourceItem item = GetSourceItem(localPath);
                localUpdates.Add(LocalUpdate.FromLocal(item.ItemId, item.LocalName, item.LocalChangesetId));
            }

            // Add children

            foreach (SourceItem item in tfsState.GetSourceItems(localPath))
            {
                if (item.ItemId != Constants.NullItemId)
                    localUpdates.Add(LocalUpdate.FromLocal(item.ItemId, item.LocalName, item.LocalChangesetId));

                if (item.ItemType == ItemType.File)
                    SetupWorkspace_Helper_File(item, pendRequests);
                else
                    SetupWorkspace_Helper_Folder(item, localUpdates, pendRequests);
            }
        }

        void SetupWorkspace_Helper_File(SourceItem item,
                                        ICollection<PendRequest> pendRequests)
        {
            switch (item.LocalItemStatus)
            {
                case SourceItemStatus.Add:
                    using (Stream stream = fileSystem.OpenFile(item.LocalName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        pendRequests.Add(PendRequest.AddFile(item.LocalName, TfsUtil.GetStreamCodePage(stream)));
                    break;

                case SourceItemStatus.Delete:
                    pendRequests.Add(PendRequest.Delete(item.LocalName));
                    break;

                case SourceItemStatus.Modified:
                    pendRequests.Add(PendRequest.Edit(item.LocalName));
                    break;
            }
        }

        void SetupWorkspace_Helper_Folder(SourceItem item,
                                          List<LocalUpdate> localUpdates,
                                          List<PendRequest> pendRequests)
        {
            switch (item.LocalItemStatus)
            {
                case SourceItemStatus.Add:
                    pendRequests.Add(PendRequest.AddFolder(item.LocalName));
                    break;

                case SourceItemStatus.Delete:
                    pendRequests.Add(PendRequest.Delete(item.LocalName));
                    break;
            }

            if (item.LocalItemStatus != SourceItemStatus.Unversioned)
                SetupWorkspace_Helper(item.LocalName, localUpdates, pendRequests);
        }
    }
}