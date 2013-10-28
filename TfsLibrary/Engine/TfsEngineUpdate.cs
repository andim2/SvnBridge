using System;
using System.Collections.Generic;
using System.IO;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        bool attemptAutoMerge = false;

        public bool AttemptAutoMerge
        {
            get { return attemptAutoMerge; }
            set { attemptAutoMerge = value; }
        }

        static void _Callback(UpdateCallback callback,
                              SourceItem item,
                              UpdateAction actionTaken)
        {
            if (callback != null)
                callback(item, actionTaken, SourceItemResult.S_Ok);
        }

        static void _Callback(UpdateCallback callback,
                              SourceItem item,
                              SourceItemResult result)
        {
            if (callback != null)
                callback(item, UpdateAction.None, result);
        }

        static void _Callback(UpdateCallback callback,
                              string localPath,
                              SourceItemResult result)
        {
            if (callback != null)
                callback(SourceItem.FromLocalPath(localPath), UpdateAction.None, result);
        }

        public virtual void Update(string localPath,
                                   bool recursive,
                                   VersionSpec version,
                                   UpdateCallback callback)
        {
            Guard.ArgumentNotNullOrEmpty(localPath, "localPath");
            Guard.ArgumentNotNull(version, "version");

            if (fileSystem.DirectoryExists(localPath))
            {
                if (!tfsState.IsFolderTracked(localPath))
                    _Callback(callback, localPath, SourceItemResult.E_NotUnderSourceControl);
                else
                    UpdateHelper(callback, localPath, localPath, recursive, version);
            }
            else
            {
                string parentDirectory = fileSystem.GetDirectoryName(localPath);

                if (!tfsState.IsFolderTracked(parentDirectory))
                    _Callback(callback, localPath, SourceItemResult.E_NotUnderSourceControl);
                else
                    UpdateHelper(callback, parentDirectory, localPath, recursive, version);
            }
        }

        void Update_File(string tfsUrl,
                         UpdateCallback callback,
                         SourceItem item)
        {
            if (item.RemoteItemStatus == SourceItemStatus.Add)
                Update_File_ServerAdd(tfsUrl, callback, item);
            else if (item.RemoteItemStatus == SourceItemStatus.Modified)
                Update_File_ServerModified(tfsUrl, callback, item);
            else if (item.RemoteItemStatus == SourceItemStatus.Delete)
                Update_File_ServerDelete(callback, item);
            else if (item.LocalItemStatus == SourceItemStatus.Missing)
                Update_File_LocalMissing(callback, item);
        }

        void Update_File_LocalMissing(UpdateCallback callback,
                                      SourceItem item)
        {
            Revert_File(item.LocalName, null);

            _Callback(callback, item, UpdateAction.Reverted);
        }

        void Update_File_ServerAdd(string tfsUrl,
                                   UpdateCallback callback,
                                   SourceItem item)
        {
            if (item.LocalItemStatus == SourceItemStatus.Add || item.LocalItemStatus == SourceItemStatus.Unversioned)
                _Callback(callback, item, SourceItemResult.E_WontClobberLocalItem);
            else
            {
                webTransferService.Download(item.DownloadUrl, GetCredentials(tfsUrl), item.LocalName);
                tfsState.TrackFile(item.LocalName, item.ItemId, item.RemoteChangesetId, SourceItemStatus.Unmodified);

                _Callback(callback, item, UpdateAction.Added);
            }
        }

        void Update_File_ServerDelete(UpdateCallback callback,
                                      SourceItem item)
        {
            if (item.LocalItemStatus == SourceItemStatus.Modified || item.LocalItemStatus == SourceItemStatus.Conflict)
                _Callback(callback, item, SourceItemResult.E_WontDeleteFileWithModifications);
            else
            {
                if (fileSystem.FileExists(item.LocalName))
                    fileSystem.DeleteFile(item.LocalName);

                string directory = fileSystem.GetDirectoryName(item.LocalName);

                if (fileSystem.DirectoryExists(directory) && tfsState.IsFolderTracked(directory))
                    tfsState.UntrackFile(item.LocalName);

                _Callback(callback, item, UpdateAction.Deleted);
            }
        }

        void Update_File_ServerModified(string tfsUrl,
                                        UpdateCallback callback,
                                        SourceItem item)
        {
            if (item.LocalItemStatus == SourceItemStatus.Conflict)
                Update_File_ServerModified_LocalConflicted(callback, item);
            else if (item.LocalItemStatus == SourceItemStatus.Modified)
                Update_File_ServerModified_LocalModified(tfsUrl, callback, item);
            else
                Update_File_ServerModified_LocalUnmodified(tfsUrl, callback, item);
        }

        static void Update_File_ServerModified_LocalConflicted(UpdateCallback callback,
                                                               SourceItem item)
        {
            _Callback(callback, item, SourceItemResult.E_AlreadyConflicted);
        }

        void Update_File_ServerModified_LocalModified(string tfsUrl,
                                                      UpdateCallback callback,
                                                      SourceItem item)
        {
            string temporaryFilename = fileSystem.CombinePath(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            try
            {
                webTransferService.Download(item.DownloadUrl, GetCredentials(tfsUrl), temporaryFilename);

                if (!AttemptAutoMerge)
                    throw new MergeConflictException();

                string[] result = MergeEngine.Merge(
                    fileSystem.ReadAllLines(item.LocalTextBaseName),
                    fileSystem.ReadAllLines(item.LocalName),
                    fileSystem.ReadAllLines(temporaryFilename));

                fileSystem.WriteAllLines(item.LocalName, result);
                tfsState.TrackFile(item.LocalName, temporaryFilename, item.ItemId, item.RemoteChangesetId, SourceItemStatus.Unmodified);

                _Callback(callback, item, UpdateAction.Merged);
            }
            catch (MergeConflictException)
            {
                string localConflictFilename = string.Format("{0}.r{1}", item.LocalName, item.LocalChangesetId);
                string serverConflictFilename = string.Format("{0}.r{1}", item.LocalName, item.RemoteChangesetId);
                string mineFilename = string.Format("{0}.mine", item.LocalName);

                fileSystem.CopyFile(item.LocalName, mineFilename);
                fileSystem.CopyFile(item.LocalTextBaseName, localConflictFilename);
                fileSystem.CopyFile(temporaryFilename, serverConflictFilename);
                fileSystem.RemoveAttributes(localConflictFilename, FileAttributes.ReadOnly);
                tfsState.MarkFileAsConflicted(item.LocalName, serverConflictFilename, item.RemoteChangesetId);

                _Callback(callback, item, UpdateAction.Conflicted);
            }
            finally
            {
                if (fileSystem.FileExists(temporaryFilename))
                    fileSystem.DeleteFile(temporaryFilename);
            }
        }

        void Update_File_ServerModified_LocalUnmodified(string tfsUrl,
                                                        UpdateCallback callback,
                                                        SourceItem item)
        {
            SourceItemStatus newStatus = SourceItemStatus.Unmodified;
            if (item.LocalItemStatus == SourceItemStatus.Delete)
                newStatus = SourceItemStatus.Delete;

            webTransferService.Download(item.DownloadUrl, GetCredentials(tfsUrl), item.LocalName);
            tfsState.TrackFile(item.LocalName, item.ItemId, item.RemoteChangesetId, newStatus);
            _Callback(callback, item, UpdateAction.Updated);
        }

        void Update_Folder(UpdateCallback callback,
                           SourceItem item)
        {
            if (item.RemoteItemStatus == SourceItemStatus.Add)
                Update_Folder_ServerAdd(callback, item);
            else if (item.RemoteItemStatus == SourceItemStatus.Modified)
                Update_Folder_ServerModified(callback, item);
            else if (item.RemoteItemStatus == SourceItemStatus.Delete)
                Update_Folder_ServerDelete(callback, item);
            else if (item.LocalItemStatus == SourceItemStatus.Missing)
                Update_Folder_LocalMissing(callback, item);
        }

        void Update_Folder_LocalMissing(UpdateCallback callback,
                                        SourceItem item)
        {
            TfsFolderInfo parentInfo = tfsState.GetFolderInfo(fileSystem.GetDirectoryName(item.LocalName));
            string serverPath = TfsUtil.CombineProjectPath(parentInfo.ServerPath, fileSystem.GetFileName(item.LocalName));

            fileSystem.EnsurePath(item.LocalName);
            tfsState.TrackFolder(parentInfo.TfsUrl, serverPath, item.LocalName, item.ItemId, item.RemoteChangesetId, SourceItemStatus.Unmodified);

            _Callback(callback, item, UpdateAction.Updated);
        }

        void Update_Folder_ServerAdd(UpdateCallback callback,
                                     SourceItem item)
        {
            TfsFolderInfo parentInfo = tfsState.GetFolderInfo(fileSystem.GetDirectoryName(item.LocalName));
            string serverPath = TfsUtil.CombineProjectPath(parentInfo.ServerPath, fileSystem.GetFileName(item.LocalName));

            fileSystem.EnsurePath(item.LocalName);
            tfsState.TrackFolder(parentInfo.TfsUrl, serverPath, item.LocalName, item.ItemId, item.RemoteChangesetId, SourceItemStatus.Unmodified);

            _Callback(callback, item, UpdateAction.Added);
        }

        static void Update_Folder_ServerDelete(UpdateCallback callback,
                                               SourceItem item)
        {
            // Untracking and deleting is actually done when we're finished

            _Callback(callback, item, UpdateAction.Deleted);
        }

        void Update_Folder_ServerModified(UpdateCallback callback,
                                          SourceItem item)
        {
            if (item.LocalItemStatus == SourceItemStatus.Unmodified || item.LocalItemStatus == SourceItemStatus.Delete)
            {
                TfsFolderInfo folderInfo = tfsState.GetFolderInfo(item.LocalName);
                tfsState.TrackFolder(folderInfo.TfsUrl, folderInfo.ServerPath, item.LocalName, item.ItemId, item.RemoteChangesetId, item.LocalItemStatus);
            }
            else if (item.LocalItemStatus == SourceItemStatus.Missing)
            {
                TfsFolderInfo parentInfo = tfsState.GetFolderInfo(fileSystem.GetDirectoryName(item.LocalName));
                string serverPath = TfsUtil.CombineProjectPath(parentInfo.ServerPath, fileSystem.GetFileName(item.LocalName));

                fileSystem.EnsurePath(item.LocalName);
                tfsState.TrackFolder(parentInfo.TfsUrl, serverPath, item.LocalName, item.ItemId, item.RemoteChangesetId, SourceItemStatus.Unmodified);
            }

            _Callback(callback, item, UpdateAction.Updated);
        }

        void UpdateHelper(UpdateCallback callback,
                          string directory,
                          string updatePath,
                          bool recursive,
                          VersionSpec version)
        {
            ValidateDirectoryStructure(directory);

            List<SourceItem> items = new List<SourceItem>();

            StatusWithServer(updatePath, version, recursive, delegate(SourceItem item,
                                                                      SourceItemResult result)
                                                             {
                                                                 if (result != SourceItemResult.S_Ok)
                                                                     _Callback(callback, item, result);
                                                                 else
                                                                     items.Add(item);
                                                             });

            List<string> deletedFolders = new List<string>();
            string tfsUrl = TfsState.GetFolderInfo(directory).TfsUrl;

            foreach (SourceItem item in items)
                if (item.ItemType == ItemType.File)
                    Update_File(tfsUrl, callback, item);
                else
                {
                    Update_Folder(callback, item);

                    if (item.RemoteItemStatus == SourceItemStatus.Delete)
                        deletedFolders.Add(item.LocalName);
                }

            // For folders that were renamed or deleted, we untrack and delete them if we can.

            deletedFolders.Sort();
            deletedFolders.Reverse();

            foreach (string directoryName in deletedFolders)
                try
                {
                    tfsState.UntrackFolder(directoryName);
                    fileSystem.DeleteDirectory(directoryName, false);
                }
                catch (IOException) {}
                catch (UnauthorizedAccessException) {}
        }

        // Callback helpers
    }
}