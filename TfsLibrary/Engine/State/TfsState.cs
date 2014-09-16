using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;

[assembly : InternalsVisibleTo("UnitTest.TfsLibrary")]

namespace CodePlex.TfsLibrary.ClientEngine
{
    class TfsState
    {
        public const string ENTRIES_FILENAME = "entries.xml";
        public const string METADATA_FOLDER = "_tfs";
        public const string TEXT_BASE_EXTENSION = ".tfs-base";
        public const string TEXT_BASE_EXTENSION_CONFLICT = ".tfs-conflict";
        public const string TEXT_BASE_FOLDER = "text-base";

        readonly IFileSystem fileSystem;

        public TfsState(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        void DeleteMetadata(string directory)
        {
            fileSystem.DeleteDirectory(GetMetadataFolder(directory), true);

            foreach (string subdirectory in fileSystem.GetDirectories(directory))
                DeleteMetadata(subdirectory);
        }

        string GetEntriesFilename(string directory)
        {
            return fileSystem.CombinePath(directory, METADATA_FOLDER, ENTRIES_FILENAME);
        }

        string GetEntriesFilenameFromShadow(string directory)
        {
            string parentDirectory = fileSystem.GetDirectoryName(directory);
            if (string.IsNullOrEmpty(parentDirectory))
                return null;

            string entriesFileName = GetEntriesFilename(parentDirectory);
            if (!fileSystem.FileExists(entriesFileName))
                entriesFileName = GetEntriesFilenameFromShadow(parentDirectory);
            if (string.IsNullOrEmpty(entriesFileName))
                return null;

            TfsStateEntryList entries = TfsStateEntryList.Deserialize(fileSystem, entriesFileName);

            TfsStateEntry entry = entries[fileSystem.GetFileName(directory)];
            if (entry != null)
                return fileSystem.CombinePath(fileSystem.UserDataPath, entry.Shadow);

            return null;
        }

        protected TfsStateEntryList GetEntryList(string directory)
        {
            string entriesFilename = GetEntriesFilename(directory);
            if (string.IsNullOrEmpty(entriesFilename))
                return new TfsStateEntryList();

            if (fileSystem.FileExists(entriesFilename))
                return TfsStateEntryList.Deserialize(fileSystem, entriesFilename);

            entriesFilename = GetEntriesFilenameFromShadow(directory);
            if (string.IsNullOrEmpty(entriesFilename))
                return new TfsStateEntryList();
            return TfsStateEntryList.Deserialize(fileSystem, entriesFilename);
        }

        public TfsFolderInfo GetFolderInfo(string directory)
        {
            TfsStateEntryList entryList = GetEntryList(directory);
            TfsStateEntry entry = entryList[""];

            if (entry == null)
                return null;

            return new TfsFolderInfo(entry.TfsServerUrl, entry.ServerPath);
        }

        public TfsStateEntry[] GetItems(string directory)
        {
            TfsStateEntryList entryList = GetEntryList(directory);

            if (entryList.Count == 0)
                throw new TfsStateException(TfsStateError.NotAWorkingFolder, directory);

            return new List<TfsStateEntry>(entryList).ToArray();
        }

        string GetMetadataFolder(string directory)
        {
            return fileSystem.CombinePath(directory, METADATA_FOLDER);
        }

        void GetMissingAndDeletedItems(string directory,
                                       List<SourceItem> result,
                                       TfsStateEntryList entries)
        {
            foreach (TfsStateEntry entry in entries)
            {
                string localPath = fileSystem.CombinePath(directory, entry.Name);

                result.Add(SourceItem.FromLocalItem(entry.ItemId,
                                                    entry.ItemType,
                                                    entry.Status == SourceItemStatus.Delete ? SourceItemStatus.Delete : SourceItemStatus.Missing,
                                                    entry.Status,
                                                    localPath,
                                                    GetTextBaseFilename(localPath),
                                                    entry.ChangesetId,
                                                    Constants.NullChangesetId,
                                                    null));
            }
        }

        public TfsStateEntry GetSelfEntry(string directory)
        {
            TfsStateEntryList entryList = GetEntryList(directory);

            if (entryList.Count == 0)
                throw new TfsStateException(TfsStateError.NotAWorkingFolder, directory);

            return entryList[""];
        }

        public SourceItem GetSourceItem(string localPath)
        {
            string parentFolder = fileSystem.GetDirectoryName(localPath);

            // Tracked folder

            if (IsFolderTracked(localPath))
            {
                if (fileSystem.DirectoryExists(localPath))
                {
                    TfsStateEntry stateEntry = GetSelfEntry(localPath);
                    return SourceItem.FromLocalDirectory(stateEntry.ItemId, stateEntry.Status, stateEntry.Status, localPath, stateEntry.ChangesetId);
                }

                TfsStateEntryList entries = GetEntryList(parentFolder);
                return GetSourceItemForFolder(localPath, entries);
            }

            // Untracked folder

            if (fileSystem.DirectoryExists(localPath))
            {
                if (!string.IsNullOrEmpty(parentFolder) && IsFolderTracked(parentFolder))
                    return SourceItem.FromLocalDirectory(Constants.NullItemId, SourceItemStatus.Unversioned, SourceItemStatus.Unversioned,
                                                         localPath, Constants.NullChangesetId);

                throw new TfsStateException(TfsStateError.NotUnderSourceControl, localPath);
            }

            // File

            if (fileSystem.FileExists(localPath) || IsFileTracked(localPath))
            {
                if (!IsFolderTracked(parentFolder))
                    throw new TfsStateException(TfsStateError.NotInAWorkingFolder, localPath);

                TfsStateEntryList entries = GetEntryList(parentFolder);
                return GetSourceItemForFile(localPath, entries);
            }

            throw new TfsStateException(TfsStateError.LocalPathNotFound, localPath);
        }

        void GetSourceItemFiles(string directory,
                                List<SourceItem> result,
                                TfsStateEntryList entries)
        {
            if (fileSystem.DirectoryExists(directory))
                foreach (string file in fileSystem.GetFiles(directory))
                    result.Add(GetSourceItemForFile(file, entries));
        }

        void GetSourceItemFolders(string directory,
                                  List<SourceItem> result,
                                  TfsStateEntryList entries)
        {
            if (fileSystem.DirectoryExists(directory))
                foreach (string folder in fileSystem.GetDirectories(directory))
                {
                    if (IsMetadataFolder(folder))
                        continue;

                    result.Add(GetSourceItemForFolder(folder, entries));
                }
        }

        SourceItem GetSourceItemForFile(string file,
                                        TfsStateEntryList entries)
        {
            string shortName = fileSystem.GetFileName(file);
            TfsStateEntry entry = entries[shortName];

            if (entry == null)
                return SourceItem.FromLocalFile(Constants.NullItemId, SourceItemStatus.Unversioned, SourceItemStatus.Unversioned,
                                                file, null, Constants.NullChangesetId, Constants.NullChangesetId, null);

            SourceItemStatus status = entry.Status;
            string conflictBaseName = null;

            if (entry.ConflictChangesetId != Constants.NullChangesetId)
                conflictBaseName = GetTextBaseConflictFilename(file);

            if (status != SourceItemStatus.Delete && status != SourceItemStatus.Conflict && !fileSystem.FileExists(file))
                status = SourceItemStatus.Missing;

            if (status == SourceItemStatus.Unmodified && IsFileModified(file))
                status = SourceItemStatus.Modified;

            entries.Delete(shortName);
            return SourceItem.FromLocalFile(entry.ItemId, status, entry.Status, file, GetTextBaseFilename(file),
                                            entry.ChangesetId, entry.ConflictChangesetId, conflictBaseName);
        }

        SourceItem GetSourceItemForFolder(string folder,
                                          TfsStateEntryList entries)
        {
            string shortName = fileSystem.GetFileName(folder);
            TfsStateEntry entry = entries[shortName];

            if (entry == null)
                return SourceItem.FromLocalDirectory(Constants.NullItemId, SourceItemStatus.Unversioned, SourceItemStatus.Unversioned,
                                                     folder, Constants.NullChangesetId);

            SourceItemStatus status = entry.Status;

            if (status != SourceItemStatus.Delete && !fileSystem.DirectoryExists(folder))
                status = SourceItemStatus.Missing;

            entries.Delete(shortName);
            return SourceItem.FromLocalDirectory(entry.ItemId, status, entry.Status, folder, entry.ChangesetId);
        }

        public SourceItem[] GetSourceItems(string directory)
        {
            Guard.ArgumentNotNullOrEmpty(directory, "directory");

            if (!IsFolderTracked(directory))
            {
                if (!fileSystem.DirectoryExists(directory))
                    throw new TfsStateException(TfsStateError.LocalPathNotFound, directory);

                throw new TfsStateException(TfsStateError.NotUnderSourceControl, directory);
            }

            List<SourceItem> result = new List<SourceItem>();
            TfsStateEntryList entries = GetEntryList(directory);
            entries.Delete(""); // Remove the self entry

            GetSourceItemFolders(directory, result, entries);
            GetSourceItemFiles(directory, result, entries);
            GetMissingAndDeletedItems(directory, result, entries);

            result.Sort();
            return result.ToArray();
        }

        string GetTextBaseConflictFilename(string filename)
        {
            return fileSystem.CombinePath(fileSystem.GetDirectoryName(filename),
                                          METADATA_FOLDER,
                                          TEXT_BASE_FOLDER,
                                          fileSystem.GetFileName(filename)) + TEXT_BASE_EXTENSION_CONFLICT;
        }

        internal string GetTextBaseFilename(string filename)
        {
            return fileSystem.CombinePath(fileSystem.GetDirectoryName(filename),
                                          METADATA_FOLDER,
                                          TEXT_BASE_FOLDER,
                                          fileSystem.GetFileName(filename)) + TEXT_BASE_EXTENSION;
        }

        string GetTextBaseFolder(string directory)
        {
            return fileSystem.CombinePath(directory, METADATA_FOLDER, TEXT_BASE_FOLDER);
        }

        bool IsFileModified(string filename)
        {
            string textBaseFilename = GetTextBaseFilename(filename);

            if (!fileSystem.FileExists(textBaseFilename))
                return true;
            if (fileSystem.GetFileSize(filename) != fileSystem.GetFileSize(textBaseFilename))
                return true;
            if (fileSystem.GetLastWriteTime(filename) == fileSystem.GetLastWriteTime(textBaseFilename))
                return false;

            return fileSystem.GetFileHashAsString(filename) != fileSystem.GetFileHashAsString(textBaseFilename);
        }

        public bool IsFileTracked(string filename)
        {
            TfsStateEntryList entryList = GetEntryList(fileSystem.GetDirectoryName(filename));
            TfsStateEntry entry = entryList[fileSystem.GetFileName(filename)];
            return (entry != null && entry.ItemType == ItemType.File);
        }

        public bool IsFolderTracked(string directory)
        {
            if (fileSystem.FileExists(GetEntriesFilename(directory)))
                return true;

            string parentFolder = fileSystem.GetDirectoryName(directory);

            if (parentFolder == null)
                return false;

            TfsStateEntryList entryList = GetEntryList(parentFolder);
            TfsStateEntry entry = entryList[fileSystem.GetFileName(directory)];
            return (entry != null && entry.ItemType == ItemType.Folder);
        }

        static bool IsMetadataFolder(string directory)
        {
            return (directory.EndsWith(Path.DirectorySeparatorChar + METADATA_FOLDER));
        }

        public void MarkConflictedFileAsResolved(string filename)
        {
            Guard.ArgumentNotNullOrEmpty(filename, "filename");

            string directory = fileSystem.GetDirectoryName(filename);
            string shortName = fileSystem.GetFileName(filename);
            TfsStateEntryList entryList = GetEntryList(directory);

            if (entryList == null)
                throw new TfsStateException(TfsStateError.NotInAWorkingFolder, filename);

            TfsStateEntry entry = entryList[shortName];

            if (entry == null)
                throw new TfsStateException(TfsStateError.NotUnderSourceControl, filename);

            TransferConflictFileToBaseFile(filename);

            entry.ChangesetId = entry.ConflictChangesetId;
            entry.ConflictChangesetId = Constants.NullChangesetId;
            entry.Status = SourceItemStatus.Unmodified;
            SaveEntryList(directory, entryList);
        }

        void MarkConflictedFileAsReverted(string filename)
        {
            Guard.ArgumentNotNullOrEmpty(filename, "filename");

            TransferConflictFileToBaseFile(filename);

            fileSystem.CopyFile(GetTextBaseFilename(filename), filename, true);
            fileSystem.RemoveAttributes(filename, FileAttributes.ReadOnly);

            string directory = fileSystem.GetDirectoryName(filename);
            string shortName = fileSystem.GetFileName(filename);
            TfsStateEntryList entryList = GetEntryList(directory);
            TfsStateEntry entry = entryList[shortName];
            entry.Status = SourceItemStatus.Unmodified;
            entry.ChangesetId = entry.ConflictChangesetId;
            entry.ConflictChangesetId = Constants.NullChangesetId;
            SaveEntryList(directory, entryList);
        }

        public void MarkFileAsConflicted(string filename,
                                         string conflictedFilename,
                                         int conflictedChangedsetId)
        {
            Guard.ArgumentNotNullOrEmpty(filename, "filename");
            Guard.ArgumentNotNullOrEmpty(conflictedFilename, "conflictedFilename");
            Guard.ArgumentValid(conflictedChangedsetId != Constants.NullChangesetId, "Changeset ID cannot be the null changset ID");

            if (!fileSystem.FileExists(filename))
                throw new TfsStateException(TfsStateError.LocalPathNotFound, filename);
            if (!fileSystem.FileExists(conflictedFilename))
                throw new TfsStateException(TfsStateError.LocalPathNotFound, conflictedFilename);

            string directory = fileSystem.GetDirectoryName(filename);

            if (!IsFolderTracked(directory))
                throw new TfsStateException(TfsStateError.NotInAWorkingFolder, filename);

            MarkFileAsConflicted_SaveConflictedBase(filename, conflictedFilename);
            MarkFileAsConflicted_UpdateEntryList(directory, filename, conflictedChangedsetId);
        }

        void MarkFileAsConflicted_SaveConflictedBase(string filename,
                                                     string conflictedFilename)
        {
            string conflictedBase = GetTextBaseConflictFilename(filename);

            if (fileSystem.FileExists(conflictedBase))
                fileSystem.RemoveAttributes(conflictedBase, FileAttributes.ReadOnly);

            fileSystem.CopyFile(conflictedFilename, conflictedBase);
            fileSystem.SetAttributes(conflictedBase, FileAttributes.ReadOnly);
        }

        void MarkFileAsConflicted_UpdateEntryList(string directory,
                                                  string filename,
                                                  int conflictedChangedsetId)
        {
            string shortName = fileSystem.GetFileName(filename);
            TfsStateEntryList entryList = GetEntryList(directory);
            TfsStateEntry entry = entryList[shortName];
            entry.ConflictChangesetId = conflictedChangedsetId;
            entry.Status = SourceItemStatus.Conflict;
            SaveEntryList(directory, entryList);
        }

        void RemoveDirectoryEntryFromParentList(string directory)
        {
            string parentDirectory = fileSystem.GetDirectoryName(directory);
            TfsStateEntryList entryList = GetEntryList(parentDirectory);
            entryList.Delete(fileSystem.GetFileName(directory));
            SaveEntryList(parentDirectory, entryList);
        }

        void RemoveFileEntryFromEntryList(string filename)
        {
            string directory = fileSystem.GetDirectoryName(filename);
            TfsStateEntryList entryList = GetEntryList(directory);
            entryList.Delete(fileSystem.GetFileName(filename));
            SaveEntryList(directory, entryList);
        }

        public void RevertFile(string filename)
        {
            Guard.ArgumentNotNull(filename, "filename");
            Guard.ArgumentValid(!fileSystem.DirectoryExists(filename), "Cannot pass a folder path (must be a filename)");

            string directoryPart = fileSystem.GetDirectoryName(filename);

            if (!IsFolderTracked(directoryPart))
                throw new TfsStateException(TfsStateError.NotInAWorkingFolder, filename);

            SourceItem item = GetSourceItem(filename);

            switch (item.LocalItemStatus)
            {
                case SourceItemStatus.Add:
                    UntrackFile(filename);
                    break;

                case SourceItemStatus.Delete:
                case SourceItemStatus.Modified:
                    RevertFile_RestoreOriginalFile(filename, item);
                    break;

                case SourceItemStatus.Missing:
                    if (item.OriginalLocalItemStatus == SourceItemStatus.Add)
                        UntrackFile(filename);
                    else
                        RevertFile_RestoreOriginalFile(filename, item);
                    break;

                case SourceItemStatus.Conflict:
                    MarkConflictedFileAsReverted(filename);
                    break;
            }
        }

        void RevertFile_RestoreOriginalFile(string filename,
                                            SourceItem item)
        {
            fileSystem.CopyFile(GetTextBaseFilename(filename), filename, true);
            fileSystem.RemoveAttributes(filename, FileAttributes.ReadOnly);
            TrackFile(filename, item.ItemId, item.LocalChangesetId, SourceItemStatus.Unmodified);
        }

        protected void SaveEntryList(string directory,
                                     TfsStateEntryList entryList)
        {
            string entriesFilename = GetEntriesFilename(directory);
            fileSystem.EnsurePath(fileSystem.GetDirectoryName(entriesFilename));
            fileSystem.EnsurePath(GetTextBaseFolder(directory));
            fileSystem.SetAttributes(fileSystem.GetDirectoryName(entriesFilename), FileAttributes.Hidden);
            entryList.Serialize(fileSystem, entriesFilename);

            string shadowFile = fileSystem.CombinePath(fileSystem.UserDataPath, entryList[""].Shadow);
            entryList.Serialize(fileSystem, shadowFile);
        }

        public void TrackFile(string filename,
                              int itemId,
                              int changesetId,
                              SourceItemStatus status)
        {
            TrackFile(filename, filename, itemId, changesetId, status);
        }

        public void TrackFile(string filename,
                              string textBaseFilename,
                              int itemId,
                              int changesetId,
                              SourceItemStatus status)
        {
            Guard.ArgumentNotNullOrEmpty(filename, "filename");
            Guard.ArgumentNotNullOrEmpty(textBaseFilename, "textBaseFilename");
            Guard.ArgumentValid(status != SourceItemStatus.Conflict, "Status cannot be SourceItemStatus.Conflict; use MarkFileAsConflicted() instead");

            string directory = fileSystem.GetDirectoryName(filename);

            if (!IsFolderTracked(directory))
                throw new TfsStateException(TfsStateError.NotInAWorkingFolder, filename);

            string shortName = fileSystem.GetFileName(filename);
            TfsStateEntryList entryList = GetEntryList(directory);
            TfsStateEntry entry = entryList[shortName];

            if (entry != null && entry.Status == SourceItemStatus.Conflict)
                throw new InvalidOperationException("Cannot TrackFile() on a file that is conflicted; use MarkFileAsResolved() first");

            if (status != SourceItemStatus.Delete)
            {
                if (!fileSystem.FileExists(filename))
                    throw new TfsStateException(TfsStateError.LocalPathNotFound, filename);

                string trackingFilename = GetTextBaseFilename(filename);

                if (fileSystem.FileExists(trackingFilename))
                    fileSystem.RemoveAttributes(trackingFilename, FileAttributes.ReadOnly);

                fileSystem.CopyFile(textBaseFilename, trackingFilename, true);
                fileSystem.SetAttributes(trackingFilename, FileAttributes.ReadOnly);
            }

            entryList[shortName] = TfsStateEntry.NewFileEntry(fileSystem.GetFileName(filename), itemId, changesetId, status);
            SaveEntryList(directory, entryList);
        }

        public void TrackFolder(string tfsUrl,
                                string serverPath,
                                string directory,
                                int itemId,
                                int changesetId,
                                SourceItemStatus status)
        {
            Guard.ArgumentNotNullOrEmpty(tfsUrl, "tfsUrl");
            Guard.ArgumentNotNullOrEmpty(serverPath, "serverPath");
            Guard.ArgumentNotNullOrEmpty(directory, "directory");

            if (!serverPath.EndsWith("/"))
                serverPath += "/";

            if (status != SourceItemStatus.Delete && !fileSystem.DirectoryExists(directory))
                throw new TfsStateException(TfsStateError.LocalPathNotFound, directory);

            TfsStateEntryList entries = GetEntryList(directory);

            if (entries.Count == 0)
                entries[""] = TfsStateEntry.NewRootEntry(tfsUrl, serverPath, itemId, changesetId, status);
            else
                entries[""] = TfsStateEntry.NewRootEntry(tfsUrl, serverPath, itemId, changesetId, status, entries[""].Shadow);

            SaveEntryList(directory, entries);

            string parentDirectory = fileSystem.GetDirectoryName(directory);

            if (!string.IsNullOrEmpty(parentDirectory) && IsFolderTracked(parentDirectory))
            {
                TfsStateEntryList parentEntries = GetEntryList(parentDirectory);
                TfsStateEntry parentEntry = TfsStateEntry.NewFolderEntry(fileSystem.GetFileName(directory), itemId, changesetId, status, entries[""].Shadow);
                parentEntries[parentEntry.Name] = parentEntry;
                SaveEntryList(parentDirectory, parentEntries);
            }
        }

        void TransferConflictFileToBaseFile(string filename)
        {
            string baseFilename = GetTextBaseFilename(filename);
            string conflictFilename = GetTextBaseConflictFilename(filename);
            fileSystem.RemoveAttributes(conflictFilename, FileAttributes.ReadOnly);
            fileSystem.RemoveAttributes(baseFilename, FileAttributes.ReadOnly);
            fileSystem.DeleteFile(baseFilename);
            fileSystem.CopyFile(conflictFilename, baseFilename);
            fileSystem.DeleteFile(conflictFilename);
            fileSystem.SetAttributes(baseFilename, FileAttributes.ReadOnly);
        }

        public void UntrackFile(string filename)
        {
            Guard.ArgumentNotNullOrEmpty(filename, "filename");

            if (!IsFolderTracked(fileSystem.GetDirectoryName(filename)))
                throw new TfsStateException(TfsStateError.NotInAWorkingFolder, filename);

            RemoveFileEntryFromEntryList(filename);

            string trackingFilename = GetTextBaseFilename(filename);

            if (fileSystem.FileExists(trackingFilename))
            {
                fileSystem.RemoveAttributes(trackingFilename, FileAttributes.ReadOnly);
                fileSystem.DeleteFile(trackingFilename);
            }

            string conflictFilename = GetTextBaseConflictFilename(filename);

            if (fileSystem.FileExists(conflictFilename))
            {
                fileSystem.RemoveAttributes(conflictFilename, FileAttributes.ReadOnly);
                fileSystem.DeleteFile(conflictFilename);
            }
        }

        public void UntrackFolder(string directory)
        {
            Guard.ArgumentNotNullOrEmpty(directory, "directory");

            if (!IsFolderTracked(directory))
                throw new TfsStateException(TfsStateError.NotUnderSourceControl, directory);

            if (fileSystem.DirectoryExists(directory))
                DeleteMetadata(directory);

            string parentDirectory = fileSystem.GetDirectoryName(directory);

            if (!string.IsNullOrEmpty(parentDirectory) && IsFolderTracked(parentDirectory))
                RemoveDirectoryEntryFromParentList(directory);
        }
    }
}