using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        public void Add(string localPath,
                        bool recursive,
                        SourceItemCallback callback)
        {
            Guard.ArgumentNotNullOrEmpty(localPath, "localPath");

            if (fileSystem.DirectoryExists(localPath))
                Add_Folder(localPath, recursive, callback, true);
            else if (fileSystem.FileExists(localPath))
                Add_File(localPath, callback, true);
            else
                _Callback(callback, localPath, SourceItemResult.E_PathNotFound);
        }

        void Add_File(string filename,
                      SourceItemCallback callback,
                      bool explicitAdd)
        {
            if (!IsParentDirectoryTracked(filename))
                _Callback(callback, filename, SourceItemResult.E_NotInAWorkingFolder);
            else if (tfsState.IsFileTracked(filename))
                _Callback(callback, filename, SourceItemResult.E_AlreadyUnderSourceControl);
            else
            {
                ValidateDirectoryStructure(fileSystem.GetDirectoryName(filename));
                Add_File_Helper(filename, callback, explicitAdd);
            }
        }

        void Add_File_Helper(string filename,
                             SourceItemCallback callback,
                             bool explicitAdd)
        {
            if (!explicitAdd && IsIgnored(filename, ItemType.File))
                return;

            tfsState.TrackFile(filename, Constants.NullItemId, Constants.NullChangesetId, SourceItemStatus.Add);
            _Callback(callback, tfsState.GetSourceItem(filename));
        }

        void Add_Folder(string directory,
                        bool recursive,
                        SourceItemCallback callback,
                        bool explicitAdd)
        {
            if (!IsParentDirectoryTracked(directory))
                _Callback(callback, directory, SourceItemResult.E_NotInAWorkingFolder);
            else if (tfsState.IsFolderTracked(directory))
                _Callback(callback, directory, SourceItemResult.E_AlreadyUnderSourceControl);
            else
            {
                ValidateDirectoryStructure(fileSystem.GetDirectoryName(directory));
                Add_Folder_Helper(directory, recursive, callback, explicitAdd);
            }
        }

        void Add_Folder_Helper(string directory,
                               bool recursive,
                               SourceItemCallback callback,
                               bool explicitAdd)
        {
            if (IsMetadataFolder(directory))
                return;

            if (!explicitAdd && IsIgnored(directory, ItemType.Folder))
                return;

            string parentFolder = fileSystem.GetDirectoryName(directory);
            string shortName = fileSystem.GetFileName(directory);
            TfsFolderInfo folderInfo = tfsState.GetFolderInfo(parentFolder);

            tfsState.TrackFolder(folderInfo.TfsUrl, folderInfo.ServerPath + shortName, directory,
                                 Constants.NullItemId, Constants.NullChangesetId, SourceItemStatus.Add);

            _Callback(callback, SourceItem.FromLocalDirectory(Constants.NullItemId, SourceItemStatus.Add, SourceItemStatus.Add,
                                                              directory, Constants.NullChangesetId));

            if (recursive)
            {
                foreach (string file in fileSystem.GetFiles(directory))
                    Add_File_Helper(file, callback, false);

                foreach (string dir in fileSystem.GetDirectories(directory))
                    Add_Folder_Helper(dir, recursive, callback, false);
            }
        }
    }
}