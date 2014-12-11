using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        public void Revert(string localPath,
                           bool recursive,
                           SourceItemCallback callback)
        {
            if (fileSystem.DirectoryExists(localPath) || tfsState.IsFolderTracked(localPath))
                Revert_Folder(localPath, recursive, callback);
            else if (fileSystem.FileExists(localPath) || tfsState.IsFileTracked(localPath))
                Revert_File(localPath, callback);
            else
                _Callback(callback, localPath, SourceItemResult.E_PathNotFound);
        }

        void Revert_File(string filename,
                         SourceItemCallback callback)
        {
            if (!IsParentDirectoryTracked(filename))
                _Callback(callback, filename, SourceItemResult.E_NotInAWorkingFolder);
            else
            {
                ValidateDirectoryStructure(fileSystem.GetDirectoryName(filename));
                Revert_File_Helper(filename, callback);
            }
        }

        void Revert_File_Helper(string filename,
                                SourceItemCallback callback)
        {
            SourceItem item = tfsState.GetSourceItem(filename);

            if (item.LocalItemStatus == SourceItemStatus.Conflict)
                CleanUpConflictArtifacts(item);

            if (item.LocalItemStatus != SourceItemStatus.Unmodified)
            {
                tfsState.RevertFile(filename);
                _Callback(callback, item, item.LocalItemStatus == SourceItemStatus.Unversioned ? SourceItemResult.E_NotUnderSourceControl : SourceItemResult.S_Ok);
            }
        }

        void Revert_Folder(string directory,
                           bool recursive,
                           SourceItemCallback callback)
        {
            if (!tfsState.IsFolderTracked(directory))
                _Callback(callback, directory, SourceItemResult.E_NotUnderSourceControl);
            else
            {
                if (fileSystem.DirectoryExists(directory))
                    ValidateDirectoryStructure(directory);
                Revert_Folder_Helper(directory, recursive, callback);
            }
        }

        void Revert_Folder_Helper(string directory,
                                  bool recursive,
                                  SourceItemCallback callback)
        {
            if (!tfsState.IsFolderTracked(directory))
                return;

            SourceItem folderItem = tfsState.GetSourceItem(directory);

            if (folderItem.LocalItemStatus == SourceItemStatus.Add)
                Revert_Folder_Helper_Add(directory, callback, folderItem);
            else if (folderItem.LocalItemStatus == SourceItemStatus.Missing)
                _Callback(callback, folderItem);
            else
            {
                if (folderItem.LocalItemStatus == SourceItemStatus.Delete)
                    Revert_Folder_Helper_Delete(directory, callback, folderItem);

                foreach (SourceItem item in tfsState.GetSourceItems(directory))
                {
                    if (item.LocalItemStatus != SourceItemStatus.Unversioned)
                    {
                        if (item.ItemType == ItemType.File)
                            Revert_File_Helper(item.LocalName, callback);
                        else if (recursive)
                            Revert_Folder_Helper(item.LocalName, recursive, callback);
                    }
                }
            }
        }

        void Revert_Folder_Helper_Add(string directory,
                                      SourceItemCallback callback,
                                      SourceItem folderItem)
        {
            tfsState.UntrackFolder(directory);
            _Callback(callback, folderItem);
        }

        void Revert_Folder_Helper_Delete(string directory,
                                         SourceItemCallback callback,
                                         SourceItem folderItem)
        {
            TfsFolderInfo folderInfo = tfsState.GetFolderInfo(directory);
            SourceItem item = tfsState.GetSourceItem(directory);
            tfsState.TrackFolder(folderInfo.TfsUrl, folderInfo.ServerPath, directory, item.ItemId,
                                 item.LocalChangesetId, SourceItemStatus.Unmodified);
            _Callback(callback, folderItem);
        }
    }
}