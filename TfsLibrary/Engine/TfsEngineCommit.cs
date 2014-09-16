using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        public int Commit(string directory,
                          string message,
                          SourceItemCallback callback)
        {
            Guard.ArgumentNotNull(directory, "directory");
            Guard.ArgumentNotNull(message, "message");

            if (!fileSystem.DirectoryExists(directory))
            {
                _Callback(callback, directory, SourceItemResult.E_DirectoryNotFound);
                return Constants.NullChangesetId;
            }

            if (!tfsState.IsFolderTracked(directory))
            {
                _Callback(callback, directory, SourceItemResult.E_NotUnderSourceControl);
                return Constants.NullChangesetId;
            }

            ValidateDirectoryStructure(directory);

            List<SourceItem> commitList = new List<SourceItem>();
            List<SourceItem> conflictList = new List<SourceItem>();

            Status(directory, VersionSpec.Latest, true, false, delegate(SourceItem item,
                                                                        SourceItemResult result)
                                                               {
                                                                   switch (item.LocalItemStatus)
                                                                   {
                                                                       case SourceItemStatus.Add:
                                                                       case SourceItemStatus.Delete:
                                                                       case SourceItemStatus.Modified:
                                                                           commitList.Add(item);
                                                                           break;

                                                                       case SourceItemStatus.Conflict:
                                                                           conflictList.Add(item);
                                                                           break;
                                                                   }
                                                               });

            if (commitList.Count == 0 && conflictList.Count == 0)
                return Constants.NullChangesetId;

            Commit_ThrowIfDeleteStateIsInconsistent(directory);
            Commit_ThrowIfConflictsExist(conflictList);

            TfsFolderInfo folderInfo = tfsState.GetFolderInfo(directory);

            using (TfsWorkspace workspace = SetupWorkspace(directory, true))
            {
                Commit_UploadFiles(directory, callback, commitList, folderInfo, workspace.Name);
                int changesetId = Commit_CommitChanges(directory, message, commitList, folderInfo, workspace.Name);
                Commit_GetAddItemIds(commitList, directory, folderInfo, false, 0);
                Commit_CleanupFiles(callback, commitList, changesetId);
                Commit_CleanupFolders(commitList, changesetId);
                return changesetId;
            }
        }

        void Commit_CleanupFile(SourceItemCallback callback,
                                SourceItem item,
                                int changesetId)
        {
            if (item.LocalItemStatus != SourceItemStatus.Delete)
            {
                tfsState.TrackFile(item.LocalName, item.ItemId, changesetId, SourceItemStatus.Unmodified);
                return;
            }

            tfsState.UntrackFile(item.LocalName);

            try
            {
                fileSystem.DeleteFile(item.LocalName);
            }
            catch (IOException) {}
            catch (UnauthorizedAccessException) {}

            _Callback(callback, item);
        }

        void Commit_CleanupFiles(SourceItemCallback callback,
                                 IEnumerable<SourceItem> commitList,
                                 int changesetId)
        {
            foreach (SourceItem item in commitList)
                if (item.ItemType == ItemType.File)
                    Commit_CleanupFile(callback, item, changesetId);
        }

        void Commit_CleanupFolder(SourceItem item,
                                  int changesetId)
        {
            if (item.LocalItemStatus != SourceItemStatus.Delete)
            {
                TfsFolderInfo fi = tfsState.GetFolderInfo(item.LocalName);
                tfsState.TrackFolder(fi.TfsUrl, fi.ServerPath, item.LocalName, item.ItemId, changesetId, SourceItemStatus.Unmodified);
                return;
            }

            tfsState.UntrackFolder(item.LocalName);

            try
            {
                fileSystem.DeleteDirectory(item.LocalName, false);
            }
            catch (IOException) {}
            catch (UnauthorizedAccessException) {}
        }

        void Commit_CleanupFolders(IEnumerable<SourceItem> commitList,
                                   int changesetId)
        {
            foreach (SourceItem item in commitList)
                if (item.ItemType == ItemType.Folder)
                    Commit_CleanupFolder(item, changesetId);
        }

        int Commit_CommitChanges(string baseDirectory,
                                 string message,
                                 IEnumerable<SourceItem> commitList,
                                 TfsFolderInfo folderInfo,
                                 string workspaceName)
        {
            List<string> commitServerList = new List<string>();
            ICredentials credentials = GetCredentials(folderInfo.TfsUrl);

            foreach (SourceItem item in commitList)
                commitServerList.Add(TfsUtil.LocalPathToServerPath(folderInfo.ServerPath,
                                                                   baseDirectory,
                                                                   item.LocalName,
                                                                   item.ItemType));

            return sourceControlService.Commit(folderInfo.TfsUrl, credentials, workspaceName, message, commitServerList, false, 0);
        }

        void Commit_GetAddItemIds(IEnumerable<SourceItem> commitList,
                                  string baseDirectory,
                                  TfsFolderInfo folderInfo,
                                  bool sortAscending,
                                  int options)
        {
            foreach (SourceItem sourceItem in commitList)
            {
                if (sourceItem.LocalItemStatus == SourceItemStatus.Add)
                {
                    string serverPath = TfsUtil.LocalPathToServerPath(folderInfo.ServerPath,
                                                                      baseDirectory,
                                                                      sourceItem.LocalName,
                                                                      sourceItem.ItemType);
                    SourceItem[] sourceItems = sourceControlService.QueryItems(folderInfo.TfsUrl,
                                                                               GetCredentials(folderInfo.TfsUrl),
                                                                               serverPath,
                                                                               RecursionType.None,
                                                                               VersionSpec.Latest,
                                                                               DeletedState.NonDeleted,
                                                                               sourceItem.ItemType,
                                                                               sortAscending,
                                                                               options);

                    sourceItem.ItemId = sourceItems[0].ItemId;
                }
            }
        }

        static void Commit_ThrowIfConflictsExist(IList<SourceItem> conflicts)
        {
            if (conflicts.Count > 0)
            {
                string[] commitNames = new string[conflicts.Count];

                for (int idx = 0; idx < conflicts.Count; ++idx)
                    commitNames[idx] = conflicts[idx].LocalName;

                throw new ConflictedCommitException(commitNames);
            }
        }

        void Commit_ThrowIfDeleteStateIsInconsistent(string directory)
        {
            bool thisDirectoryIsDeleted = tfsState.GetSourceItem(directory).LocalItemStatus == SourceItemStatus.Delete;

            if (fileSystem.DirectoryExists(directory))
            {
                foreach (SourceItem item in tfsState.GetSourceItems(directory))
                {
                    if (thisDirectoryIsDeleted)
                        if (item.LocalItemStatus != SourceItemStatus.Delete && item.LocalItemStatus != SourceItemStatus.Unversioned)
                            throw new InconsistentTfsStateException(directory);

                    if (item.ItemType == ItemType.Folder && item.LocalItemStatus != SourceItemStatus.Unversioned)
                        Commit_ThrowIfDeleteStateIsInconsistent(item.LocalName);
                }
            }
        }

        void Commit_UploadFiles(string baseDirectory,
                                SourceItemCallback callback,
                                IEnumerable<SourceItem> commitList,
                                TfsFolderInfo folderInfo,
                                string workspaceName)
        {
            foreach (SourceItem item in commitList)
            {
                string serverPath = TfsUtil.LocalPathToServerPath(folderInfo.ServerPath,
                                                                  baseDirectory,
                                                                  item.LocalName,
                                                                  item.ItemType);

                if (item.ItemType == ItemType.File && item.LocalItemStatus != SourceItemStatus.Delete)
                {
                    sourceControlService.UploadFile(folderInfo.TfsUrl, GetCredentials(folderInfo.TfsUrl), workspaceName, item.LocalName, serverPath);
                    _Callback(callback, item);
                }
            }
        }
    }
}