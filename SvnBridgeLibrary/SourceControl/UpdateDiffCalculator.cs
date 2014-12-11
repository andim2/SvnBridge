using System;
using System.Collections.Generic;
using System.IO;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Interfaces;
using SvnBridge.Protocol;
using SvnBridge.Utility;
using SvnBridge.Infrastructure;

namespace SvnBridge.SourceControl
{
    public class UpdateDiffCalculator
    {
        private readonly TFSSourceControlProvider sourceControlProvider;
        private Dictionary<string, int> clientExistingFiles;
        private Dictionary<string, string> clientMissingFiles;
        private readonly Dictionary<ItemMetaData, bool> additionForPropertyChangeOnly = new Dictionary<ItemMetaData,bool>();
        private readonly List<string> renamedItemsToBeCheckedForDeletedChildren = new List<string>();

        public UpdateDiffCalculator(TFSSourceControlProvider sourceControlProvider)
        {
            this.sourceControlProvider = sourceControlProvider;
        }

        public void CalculateDiff(string checkoutRootPath, int versionTo, int versionFrom, FolderMetaData checkoutRoot, UpdateReportData updateReportData)
        {
            clientExistingFiles = GetClientExistingFiles(checkoutRootPath, updateReportData);
            clientMissingFiles = GetClientDeletedFiles(checkoutRootPath, updateReportData);
            string projectRootPath = GetProjectRoot(checkoutRootPath);

            if (updateReportData.Entries != null)
            {
                foreach (EntryData data in updateReportData.Entries)
                {
                    int itemVersionFrom = int.Parse(data.Rev);
                    if (itemVersionFrom < versionFrom)
                    {
                        string rootPath = checkoutRootPath;
                        if (updateReportData.UpdateTarget != null)
                            rootPath += "/" + updateReportData.UpdateTarget;

                        string targetPath = rootPath + "/" + data.path;

                        if (targetPath.StartsWith("/"))
                            targetPath = targetPath.Substring(1);

                        CalculateChangeBetweenVersions(projectRootPath, targetPath, itemVersionFrom, checkoutRoot, itemVersionFrom, versionFrom);
                    }
                }
            }

            if (versionFrom != versionTo)
            {
                // we have to calculate the difference from the project root
                // this is because we may have a file move from below the checkoutRootPath, 
                // which we still need to consider
                FolderMetaData projectRoot = checkoutRoot;
                if (projectRootPath != checkoutRootPath)
                {
                    projectRoot = (FolderMetaData)sourceControlProvider.GetItems(versionTo, projectRootPath, Recursion.None);
                    string path = checkoutRootPath.Substring(0, checkoutRootPath.LastIndexOf('/'));
                    path = path.Substring(path.IndexOf('/') + 1);
                    FolderMetaData result = (FolderMetaData)FindItemOrCreateItem(projectRoot, projectRootPath, path, versionTo, Recursion.None);
                    result.Items.Add(checkoutRoot);
                }

                CalculateChangeBetweenVersions(projectRootPath, -1, projectRoot, versionFrom, versionTo);
            }

            foreach (string missingItem in clientMissingFiles.Values)
            {
                if (sourceControlProvider.ItemExists(checkoutRootPath + "/" + missingItem, versionTo))
                {
                    FindItemOrCreateItem(checkoutRoot, checkoutRootPath, missingItem, versionTo, Recursion.Full);
                }
            }
            FlattenDeletedFolders(checkoutRoot);
            RemoveMissingItemsWhichAreChildrenOfRenamedItem(checkoutRoot);
            VerifyNoMissingItemMetaDataRemained(checkoutRoot);
        }

        private static string GetProjectRoot(string path)
        {
            string[] parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "";
            return parts[0];
        }

        private void RemoveMissingItemsWhichAreChildrenOfRenamedItem(FolderMetaData root)
        {
            foreach (string item in renamedItemsToBeCheckedForDeletedChildren)
            {
                RemoveMissingItemsWhichAreChildrenOfRenamedItem(item, root);
            }
        }

        private static void VerifyNoMissingItemMetaDataRemained(FolderMetaData root)
        {
            foreach (ItemMetaData item in root.Items)
            {
                if (item is MissingItemMetaData)
                    throw new InvalidOperationException("Found missing item:" + item +
                                                        " but those should not be returned from UpdateDiffCalculator");
                if (item is FolderMetaData)
                    VerifyNoMissingItemMetaDataRemained((FolderMetaData)item);
            }
        }

        private ItemMetaData FindItemOrCreateItem(FolderMetaData root, string pathRoot, string path, int targetVersion, Recursion recursion)
        {
            FolderMetaData folder = root;
            string[] parts = path.Split('/');
            string itemName = pathRoot;
            ItemMetaData item = null;
            for (int i = 0; i < parts.Length; i++)
            {
                if (itemName != "" && !itemName.EndsWith("/"))
                    itemName += "/" + parts[i];
                else
                    itemName += parts[i];

                item = folder.FindItem(itemName);
                bool lastNamePart = i == parts.Length - 1;
                if (item == null)
                {
                    if (lastNamePart)
                    {
                        item = sourceControlProvider.GetItems(targetVersion, itemName, recursion);
                    }
                    else
                    {
                        FolderMetaData subFolder =
                            (FolderMetaData)sourceControlProvider.GetItems(targetVersion, itemName, recursion);
                        item = subFolder;
                    }
                    item = item ?? new MissingItemMetaData(itemName, targetVersion, false);
                    folder.Items.Add(item);
                }
                if (lastNamePart == false)
                {
                    folder = (FolderMetaData)item;
                }
            }
            return item;
        }

        private void CalculateChangeBetweenVersions(string checkoutRootPath, int checkoutRootVersion, FolderMetaData root, int sourceVersion, int targetVersion)
        {
            CalculateChangeBetweenVersions(checkoutRootPath, checkoutRootPath, checkoutRootVersion, root, sourceVersion, targetVersion);
        }

        private void CalculateChangeBetweenVersions(string checkoutRootPath, string changePath, int changeVersion, FolderMetaData root, int sourceVersion, int targetVersion)
        {
            bool updatingForwardInTime = sourceVersion <= targetVersion;
            int lastVersion = sourceVersion;
            while (targetVersion != lastVersion)
            {
                int previousLoopLastVersion = lastVersion;
                LogItem logItem = sourceControlProvider.GetLog(
                    changePath,
                    changeVersion,
                    Math.Min(lastVersion, targetVersion) + 1,
                    Math.Max(lastVersion, targetVersion),
                    Recursion.Full, 256);

                foreach (SourceItemHistory history in Helper.SortHistories(updatingForwardInTime, logItem.History))
                {
                    lastVersion = history.ChangeSetID;
                    if (updatingForwardInTime == false)
                    {
                        lastVersion -= 1;
                    }

                    // we need to go over the changeset in reverse order so we will process
                    // all the files first, and build the folder hierarchy that way
                    for (int i = history.Changes.Count - 1; i >= 0; i--)
                    {
                        UpdateDiffEngine engine = new UpdateDiffEngine(root, checkoutRootPath, targetVersion, sourceControlProvider, clientExistingFiles, clientMissingFiles, additionForPropertyChangeOnly, renamedItemsToBeCheckedForDeletedChildren);
                        SourceItemChange change = history.Changes[i];
                        if (ShouldBeIgnored(change.Item.RemoteName))
                            continue;
                        if (IsAddOperation(change, updatingForwardInTime))
                        {
                            engine.Add(change);
                        }
                        else if (IsDeleteOperation(change, updatingForwardInTime))
                        {
                            engine.Delete(change);
                        }
                        else if (IsEditOperation(change))
                        {
                            // We may have edit & rename operations
                            if (IsRenameOperation(change))
                            {
                                engine.Rename(change, updatingForwardInTime);
                            }
                            if (updatingForwardInTime == false)
                            {
                                change.Item.RemoteChangesetId -= 1; // we turn the edit around, basically
                            }
                            engine.Edit(change);
                        }
                        else if (IsRenameOperation(change))
                        {
                            engine.Rename(change, updatingForwardInTime);
                        }
                        else
                        {
                            throw new NotSupportedException("Unsupported change type " + change.ChangeType);
                        }
                    }
                }
                // No change was made, break out
                if (previousLoopLastVersion == lastVersion)
                {
                    break;
                }
            }
        }

        private static void RemoveMissingItemsWhichAreChildrenOfRenamedItem(string itemName, FolderMetaData root)
        {
            if (itemName.StartsWith("/"))
                itemName = itemName.Substring(1);

            foreach (ItemMetaData data in new List<ItemMetaData>(root.Items))
            {
                string nameMatchingSourceItemConvention = data.Name;
                if (data.Name.StartsWith("/"))
                    nameMatchingSourceItemConvention = data.Name.Substring(1);

                // a child of the currently renamed item
                if (data is MissingItemMetaData &&
                    nameMatchingSourceItemConvention.StartsWith(itemName, StringComparison.InvariantCultureIgnoreCase))
                {
                    root.Items.Remove(data);
                    continue;
                }
                if (data is FolderMetaData)
                {
                    RemoveMissingItemsWhichAreChildrenOfRenamedItem(itemName, (FolderMetaData)data);
                }
            }
        }

        private static bool IsRenameOperation(SourceItemChange change)
        {
            return (change.ChangeType & ChangeType.Rename) == ChangeType.Rename;
        }

        private static bool IsDeleteOperation(SourceItemChange change, bool updatingForwardInTime)
        {
            if (updatingForwardInTime == false)
            {
                return IsAddOperation(change, true);
            }
            return (change.ChangeType & ChangeType.Delete) == ChangeType.Delete;
        }

        private static bool IsAddOperation(SourceItemChange change, bool updatingForwardInTime)
        {
            if (updatingForwardInTime == false)
            {
                return IsDeleteOperation(change, true);
            }
            return ((change.ChangeType & ChangeType.Add) == ChangeType.Add) ||
                   ((change.ChangeType & ChangeType.Branch) == ChangeType.Branch) ||
                   ((change.ChangeType & ChangeType.Undelete) == ChangeType.Undelete);
        }

        private static bool IsEditOperation(SourceItemChange change)
        {
            return (change.ChangeType & ChangeType.Edit) == ChangeType.Edit;
        }

        private static Dictionary<string, string> GetClientDeletedFiles(string path, UpdateReportData reportData)
        {
            Dictionary<string, string> clientDeletedFiles = new Dictionary<string, string>();
            if (reportData.Missing != null)
            {
                foreach (string missingPath in reportData.Missing)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        clientDeletedFiles["/" + missingPath] = missingPath;
                    }
                    else
                    {
                        clientDeletedFiles["/" + path + "/" + missingPath] = missingPath;
                    }
                }
            }
            return clientDeletedFiles;
        }

        private static Dictionary<string, int> GetClientExistingFiles(string path, UpdateReportData reportData)
        {
            Dictionary<string, int> clientExistingFiles = new Dictionary<string, int>();
            if (reportData.Entries != null)
            {
                foreach (EntryData entryData in reportData.Entries)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        clientExistingFiles["/" + entryData.path] = int.Parse(entryData.Rev);
                    }
                    else
                    {
                        clientExistingFiles["/" + path + "/" + entryData.path] = int.Parse(entryData.Rev);
                    }
                }
            }
            return clientExistingFiles;
        }

        /// This method ensures that we are not sending useless deletes to the client
        /// if a folder is to be deleted, all its children are as well, which we remove
        /// at this phase.
        private static void FlattenDeletedFolders(FolderMetaData parentFolder)
        {
            foreach (ItemMetaData item in parentFolder.Items)
            {
                FolderMetaData folder = item as FolderMetaData;
                if (folder == null)
                {
                    continue;
                }
                if (folder is DeleteFolderMetaData)
                {
                    folder.Items.Clear();
                }
                else
                {
                    FlattenDeletedFolders(folder);
                }
            }
        }

        private bool ShouldBeIgnored(string file)
        {
            return Path.GetFileName(file) == "..svnbridge";
        }
   }
}