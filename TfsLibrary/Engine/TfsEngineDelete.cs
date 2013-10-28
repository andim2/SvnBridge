using System;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        public void Delete(string localPath,
                           bool force,
                           SourceItemCallback callback)
        {
            Guard.ArgumentNotNullOrEmpty(localPath, "localPath");

            if (fileSystem.FileExists(localPath) || tfsState.IsFileTracked(localPath))
                Delete_File(localPath, force, callback);
            else if (fileSystem.DirectoryExists(localPath) || tfsState.IsFolderTracked(localPath))
                Delete_Folder(localPath, force, callback);
            else
                _Callback(callback, localPath, SourceItemResult.E_PathNotFound);
        }

        void Delete_File(string filename,
                         bool force,
                         SourceItemCallback callback)
        {
            if (!IsParentDirectoryTracked(filename))
                _Callback(callback, filename, SourceItemResult.E_NotInAWorkingFolder);
            else
            {
                ValidateDirectoryStructure(fileSystem.GetDirectoryName(filename));
                Delete_File_Helper(filename, force, callback);
            }
        }

        void Delete_File_Helper(string filename,
                                bool force,
                                SourceItemCallback callback)
        {
            if (tfsState.IsFileTracked(filename))
                Delete_File_Tracked(filename, force, callback);
            else
                Delete_File_Untracked(filename, force, callback);
        }

        void Delete_File_Tracked(string filename,
                                 bool force,
                                 SourceItemCallback callback)
        {
            SourceItem item = tfsState.GetSourceItem(filename);

            switch (item.LocalItemStatus)
            {
                case SourceItemStatus.Add:
                case SourceItemStatus.Modified:
                    Delete_File_Tracked_AddOrModified(filename, item, force, callback);
                    break;

                case SourceItemStatus.Missing:
                case SourceItemStatus.Unmodified:
                    Delete_File_Tracked_UnmodifiedOrMissing(filename, item, callback);
                    break;

                case SourceItemStatus.Delete: // Already deleted, just re-issue the callback
                    _Callback(callback, item);
                    break;

                default:
                    throw new InvalidOperationException("Unexpected status " + item.LocalItemStatus + " for " + filename);
            }
        }

        void Delete_File_Tracked_AddOrModified(string filename,
                                               SourceItem item,
                                               bool force,
                                               SourceItemCallback callback)
        {
            if (force)
            {
                if (item.LocalItemStatus == SourceItemStatus.Add)
                    tfsState.UntrackFile(filename);
                else
                    tfsState.TrackFile(filename, item.ItemId, item.LocalChangesetId, SourceItemStatus.Delete);

                fileSystem.DeleteFile(filename);
                item.LocalItemStatus = SourceItemStatus.Delete;
                _Callback(callback, item, SourceItemResult.S_ForcedDelete);
            }
            else
                _Callback(callback, filename, SourceItemResult.E_HasLocalModifications);
        }

        void Delete_File_Tracked_UnmodifiedOrMissing(string filename,
                                                     SourceItem item,
                                                     SourceItemCallback callback)
        {
            item.LocalItemStatus = SourceItemStatus.Delete;
            tfsState.TrackFile(filename, item.ItemId, item.LocalChangesetId, SourceItemStatus.Delete);

            if (fileSystem.FileExists(filename))
                fileSystem.DeleteFile(filename);

            _Callback(callback, item);
        }

        void Delete_File_Untracked(string filename,
                                   bool force,
                                   SourceItemCallback callback)
        {
            if (force)
            {
                fileSystem.DeleteFile(filename);
                _Callback(callback, filename, SourceItemResult.S_ForcedDelete);
            }
            else
                _Callback(callback, filename, SourceItemResult.E_NotUnderSourceControl);
        }

        void Delete_Folder(string directory,
                           bool force,
                           SourceItemCallback callback)
        {
            if (IsMetadataFolder(directory))
                return;

            if (!IsParentDirectoryTracked(directory))
                _Callback(callback, directory, SourceItemResult.E_NotInAWorkingFolder);
            else
            {
                ValidateDirectoryStructure(fileSystem.GetDirectoryName(directory));
                Delete_Folder_Helper(directory, force, callback);
            }
        }

        bool Delete_Folder_DeleteChildren(string directory,
                                          bool force,
                                          SourceItemCallback callback)
        {
            bool hasChildErrors = false;

            foreach (SourceItem fileItem in tfsState.GetSourceItems(directory))
                if (fileItem.ItemType == ItemType.File)
                    Delete_File_Helper(fileItem.LocalName, force,
                                       delegate(SourceItem i,
                                                SourceItemResult r)
                                       {
                                           if (r != SourceItemResult.S_Ok && r != SourceItemResult.S_ForcedDelete)
                                               hasChildErrors = true;
                                           _Callback(callback, i, r);
                                       });
                else
                    Delete_Folder_Helper(fileItem.LocalName, force,
                                         delegate(SourceItem i,
                                                  SourceItemResult r)
                                         {
                                             if (r != SourceItemResult.S_Ok && r != SourceItemResult.S_ForcedDelete)
                                                 hasChildErrors = true;
                                             _Callback(callback, i, r);
                                         });

            return hasChildErrors;
        }

        void Delete_Folder_Helper(string directory,
                                  bool force,
                                  SourceItemCallback callback)
        {
            if (IsMetadataFolder(directory))
                return;

            if (tfsState.IsFolderTracked(directory))
                Delete_Folder_Tracked(directory, force, callback);
            else
                Delete_Folder_Untracked(directory, force, callback);
        }

        void Delete_Folder_Tracked(string directory,
                                   bool force,
                                   SourceItemCallback callback)
        {
            SourceItem item = tfsState.GetSourceItem(directory);

            switch (item.LocalItemStatus)
            {
                case SourceItemStatus.Add:
                    Delete_Folder_Tracked_Add(directory, item, force, callback);
                    break;

                case SourceItemStatus.Missing:
                case SourceItemStatus.Unmodified:
                    Delete_Folder_Tracked_UnmodifiedOrMissing(directory, item, force, callback);
                    break;

                case SourceItemStatus.Delete: // Already deleted, just re-issue the callback
                    _Callback(callback, item);
                    break;

                default:
                    throw new InvalidOperationException("Unexpected status " + item.LocalItemStatus + " for " + directory);
            }
        }

        void Delete_Folder_Tracked_Add(string directory,
                                       SourceItem item,
                                       bool force,
                                       SourceItemCallback callback)
        {
            if (force)
            {
                item.LocalItemStatus = SourceItemStatus.Delete;
                tfsState.UntrackFolder(directory);
                fileSystem.DeleteDirectory(directory, true);
                _Callback(callback, item);
            }
            else
                _Callback(callback, item, SourceItemResult.E_HasLocalModifications);
        }

        void Delete_Folder_Tracked_UnmodifiedOrMissing(string directory,
                                                       SourceItem item,
                                                       bool force,
                                                       SourceItemCallback callback)
        {
            bool hasChildErrors = Delete_Folder_DeleteChildren(directory, force, callback);

            if (hasChildErrors)
                _Callback(callback, item, SourceItemResult.E_ChildDeleteFailure);
            else
            {
                TfsFolderInfo folderInfo = tfsState.GetFolderInfo(directory);
                string tfsUrl;
                string serverPath;

                if (folderInfo == null)
                {
                    folderInfo = tfsState.GetFolderInfo(fileSystem.GetDirectoryName(directory));
                    tfsUrl = folderInfo.TfsUrl;
                    serverPath = TfsUtil.CombineProjectPath(folderInfo.ServerPath, fileSystem.GetFileName(directory));
                }
                else
                {
                    tfsUrl = folderInfo.TfsUrl;
                    serverPath = folderInfo.ServerPath;
                }

                tfsState.TrackFolder(tfsUrl, serverPath, directory, item.ItemId, item.LocalChangesetId, SourceItemStatus.Delete);

                item.LocalItemStatus = SourceItemStatus.Delete;
                _Callback(callback, item);
            }
        }

        void Delete_Folder_Untracked(string directory,
                                     bool force,
                                     SourceItemCallback callback)
        {
            if (force)
            {
                fileSystem.DeleteDirectory(directory, true);
                _Callback(callback, directory, SourceItemResult.S_ForcedDelete);
            }
            else
                _Callback(callback, directory, SourceItemResult.E_NotUnderSourceControl);
        }
    }
}