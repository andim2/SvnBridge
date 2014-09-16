using System.Collections.Generic;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        static void _Callback(DiffCallback callback,
                              string localPath,
                              SourceItemResult result)
        {
            if (callback != null)
                callback(localPath, null, null, null, null, result);
        }

        static void _Callback(DiffCallback callback,
                              string leftPathname,
                              string leftVersion,
                              string rightPathname,
                              string rightVersion,
                              List<DiffEngine.Chunk> diff,
                              SourceItemResult result)
        {
            if (callback != null)
                callback(leftPathname, leftVersion, rightPathname, rightVersion, diff, result);
        }

        public void Diff(string localPath,
                         bool recursive,
                         DiffCallback callback)
        {
            Guard.ArgumentNotNull(localPath, "localPath");
            Guard.ArgumentNotNull(callback, "callback");

            if (fileSystem.FileExists(localPath) || tfsState.IsFileTracked(localPath))
                Diff_File(localPath, callback);
            else if (fileSystem.DirectoryExists(localPath) || tfsState.IsFolderTracked(localPath))
                Diff_Folder(localPath, recursive, callback);
            else
                _Callback(callback, localPath, SourceItemResult.E_PathNotFound);
        }

        void Diff_File(string filename,
                       DiffCallback callback)
        {
            if (!IsParentDirectoryTracked(filename))
                _Callback(callback, filename, SourceItemResult.E_NotInAWorkingFolder);
            else
            {
                SourceItem sourceItem = tfsState.GetSourceItem(filename);

                switch (sourceItem.LocalItemStatus)
                {
                    case SourceItemStatus.Unversioned:
                        _Callback(callback, filename, SourceItemResult.E_NotUnderSourceControl);
                        break;

                    case SourceItemStatus.Modified:
                        Diff_File_Modified(sourceItem, filename, callback);
                        break;

                    case SourceItemStatus.Add:
                        Diff_File_Added(filename, callback);
                        break;

                    case SourceItemStatus.Delete:
                        Diff_File_Delete(sourceItem, filename, callback);
                        break;

                    case SourceItemStatus.Conflict:
                        Diff_File_Conflict(sourceItem, filename, callback);
                        break;
                }
            }
        }

        void Diff_File_Added(string filename,
                             DiffCallback callback)
        {
            string[] lines = fileSystem.ReadAllLines(filename);
            List<DiffEngine.Chunk> diff = new List<DiffEngine.Chunk>();
            diff.Add(new DiffEngine.Chunk(DiffEngine.ChunkType.Right, lines, 0, lines.Length - 1));

            _Callback(callback, filename, Diff_GetRevisionText(0), filename, Diff_GetRevisionText(0), diff, SourceItemResult.S_Ok);
        }

        void Diff_File_Conflict(SourceItem sourceItem,
                                string filename,
                                DiffCallback callback)
        {
            List<DiffEngine.Chunk> diff = DiffEngine.GetDiff(fileSystem.ReadAllLines(sourceItem.LocalConflictTextBaseName),
                                                             fileSystem.ReadAllLines(filename));

            _Callback(callback, filename, Diff_GetRevisionText(sourceItem.LocalConflictChangesetId),
                      filename, "working copy", diff, SourceItemResult.S_Ok);
        }

        void Diff_File_Delete(SourceItem sourceItem,
                              string filename,
                              DiffCallback callback)
        {
            string[] lines = fileSystem.ReadAllLines(sourceItem.LocalTextBaseName);
            List<DiffEngine.Chunk> diff = new List<DiffEngine.Chunk>();
            diff.Add(new DiffEngine.Chunk(DiffEngine.ChunkType.Left, lines, 0, lines.Length - 1));

            _Callback(callback, filename, Diff_GetRevisionText(sourceItem.LocalChangesetId), filename, "working copy", diff, SourceItemResult.S_Ok);
        }

        void Diff_File_Modified(SourceItem sourceItem,
                                string filename,
                                DiffCallback callback)
        {
            List<DiffEngine.Chunk> diff = DiffEngine.GetDiff(fileSystem.ReadAllLines(sourceItem.LocalTextBaseName),
                                                             fileSystem.ReadAllLines(filename));

            _Callback(callback, filename, Diff_GetRevisionText(sourceItem.LocalChangesetId),
                      filename, "working copy", diff, SourceItemResult.S_Ok);
        }

        void Diff_Folder(string directory,
                         bool recursive,
                         DiffCallback callback)
        {
            if (!tfsState.IsFolderTracked(directory))
                _Callback(callback, directory, SourceItemResult.E_NotUnderSourceControl);
            else
                Diff_Folder_Helper(directory, recursive, callback);
        }

        void Diff_Folder_Helper(string directory,
                                bool recursive,
                                DiffCallback callback)
        {
            foreach (SourceItem item in tfsState.GetSourceItems(directory))
                if (item.ItemType == ItemType.Folder)
                {
                    if (recursive && tfsState.IsFolderTracked(item.LocalName) && item.LocalItemStatus != SourceItemStatus.Missing)
                        Diff_Folder_Helper(item.LocalName, recursive, callback);
                }
                else
                {
                    if (item.LocalItemStatus != SourceItemStatus.Unversioned)
                        Diff_File(item.LocalName, callback);
                }
        }

        static string Diff_GetRevisionText(int changesetId)
        {
            return string.Format("revision {0}", changesetId);
        }

        // Callback helpers
    }
}