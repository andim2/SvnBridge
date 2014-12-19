using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        public void Resolve(string localPath,
                            bool recursive,
                            SourceItemCallback callback)
        {
            Guard.ArgumentNotNullOrEmpty(localPath, "localPath");

            if (fileSystem.DirectoryExists(localPath) || tfsState.IsFolderTracked(localPath))
                Resolve_Folder(localPath, recursive, callback);
            else if (fileSystem.FileExists(localPath) || tfsState.IsFileTracked(localPath))
                Resolve_File(localPath, callback);
            else
                _Callback(callback, localPath, SourceItemResult.E_PathNotFound);
        }

        void Resolve_File(string filename,
                          SourceItemCallback callback)
        {
            if (!IsParentDirectoryTracked(filename))
                _Callback(callback, filename, SourceItemResult.E_NotInAWorkingFolder);
            else
                Resolve_File_Helper(filename, callback);
        }

        void Resolve_File_Helper(string filename,
                                 SourceItemCallback callback)
        {
            SourceItem item = tfsState.GetSourceItem(filename);

            if (item.LocalItemStatus == SourceItemStatus.Conflict)
            {
                tfsState.MarkConflictedFileAsResolved(filename);
                CleanUpConflictArtifacts(item);

                _Callback(callback, tfsState.GetSourceItem(filename), SourceItemResult.S_Ok);
            }
            else if (item.LocalItemStatus == SourceItemStatus.Unversioned)
            {
                _Callback(callback, item, SourceItemResult.E_NotUnderSourceControl);
            }
        }

        void Resolve_Folder(string directory,
                            bool recursive,
                            SourceItemCallback callback)
        {
            if (!tfsState.IsFolderTracked(directory))
                _Callback(callback, directory, SourceItemResult.E_NotUnderSourceControl);
            else
                Resolve_Folder_Helper(directory, recursive, callback);
        }

        void Resolve_Folder_Helper(string directory,
                                   bool recursive,
                                   SourceItemCallback callback)
        {
            foreach (SourceItem item in tfsState.GetSourceItems(directory))
                if (item.ItemType == ItemType.Folder)
                {
                    if (recursive && tfsState.IsFolderTracked(item.LocalName) && item.LocalItemStatus != SourceItemStatus.Missing)
                        Resolve_Folder_Helper(item.LocalName, recursive, callback);
                }
                else
                {
                    if (item.LocalItemStatus == SourceItemStatus.Conflict)
                        Resolve_File_Helper(item.LocalName, callback);
                }
        }
    }
}