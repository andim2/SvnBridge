using System;
using System.Collections.Generic;
using System.IO; // Path.GetFileName()
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Infrastructure; // Configuration
using SvnBridge.Protocol;
using SvnBridge.Utility; // Helper.SortHistories()

namespace SvnBridge.SourceControl
{
    public class UpdateDiffCalculator
    {
        private readonly TFSSourceControlProvider sourceControlProvider;
        private Dictionary<string, int> clientExistingFiles;
        private Dictionary<string, string> clientMissingFiles;
        private readonly Dictionary<ItemMetaData, bool> additionForPropertyChangeOnly = new Dictionary<ItemMetaData, bool>();
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

                        FilesysHelpers.StripRootSlash(ref targetPath);

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
            checkoutRoot.VerifyNoMissingItemMetaDataRemained();
        }

        private static string GetProjectRoot(string path)
        {
            string[] parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "";
            return parts[0];
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
            while (lastVersion != targetVersion)
            {
                int previousLoopLastVersion = lastVersion;
                int versionFrom = Math.Min(lastVersion, targetVersion) + 1;
                int versionTo = Math.Max(lastVersion, targetVersion);
                var historiesSorted = FetchSortedHistory(changePath, changeVersion, versionFrom, versionTo, updatingForwardInTime);

                CalculateChangeViaSourceItemHistories(historiesSorted, checkoutRootPath, root, updatingForwardInTime, ref lastVersion);
                // No change was made, break out
                if (previousLoopLastVersion == lastVersion)
                {
                    break;
                }
            }
        }

        private IList<SourceItemHistory> FetchSortedHistory(string changePath, int changeVersion, int versionFrom, int versionTo, bool updatingForwardInTime)
        {
            IList<SourceItemHistory> historiesSorted = null;

            LogItem logItem = sourceControlProvider.GetLog(
                changePath,
                changeVersion,
                versionFrom, versionTo,
                Recursion.Full,
                256);

            historiesSorted = Helper.SortHistories(updatingForwardInTime, logItem.History);

            return historiesSorted;
        }

        private void CalculateChangeViaSourceItemHistories(IList<SourceItemHistory> historiesSorted, string checkoutRootPath, FolderMetaData root, bool updatingForwardInTime, ref int lastVersion)
        {
            foreach (SourceItemHistory history in historiesSorted)
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
                    SourceItemChange change = history.Changes[i];

                    if (ShouldBeIgnored(change.Item.RemoteName))
                        continue;

                    // Fixed MEGA BUG: the UpdateDiffEngine's "target" version
                    // should be the one of the current Changeset i.e. lastVersion!!
                    // (since this specific loop iteration is supposed to apply changes relevant to this Changeset only,
                    // and *not* the targetVersion of the global loop processing).
                    UpdateDiffEngine engine = new UpdateDiffEngine(root, checkoutRootPath, lastVersion, sourceControlProvider, clientExistingFiles, clientMissingFiles, additionForPropertyChangeOnly, renamedItemsToBeCheckedForDeletedChildren);

                    ApplyChangeOps(engine, change, updatingForwardInTime);
                }
            }
        }

        /// <summary>
        /// Temporary(?) helper. It probably should be refactored to become a method of UpdateDiffEngine instead.
        /// Well, no: that would mean having to move all Is*Operation() helpers into the engine as well,
        /// which is a bad idea. Hmm, OTOH all uses of such helpers are exclusively for engine processing,
        /// plus the engine has some identical (duplicated) helpers as well.........
        /// Since engine impl *is* dependent on TfsLibrary-side SourceItemChange knowledge,
        /// perhaps this *is* the proper thing to do after all.
        /// </summary>
        private void ApplyChangeOps(UpdateDiffEngine engine, SourceItemChange change, bool updatingForwardInTime)
        {
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

        /// <summary>
        /// This method ensures that we are not sending useless deletes to the client -
        /// if a folder is to be deleted, all its children are as well, which we remove
        /// at this phase.
        /// </summary>
        /// <param name="parentFolder">Folder where any deleted items ought to be recursively removed from</param>
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

        private void RemoveMissingItemsWhichAreChildrenOfRenamedItem(FolderMetaData root)
        {
            foreach (string item in renamedItemsToBeCheckedForDeletedChildren)
            {
                RemoveMissingItemsWhichAreChildrenOfRenamedItem(item, root);
            }
        }

        private static void RemoveMissingItemsWhichAreChildrenOfRenamedItem(string itemName, FolderMetaData root)
        {
            FilesysHelpers.StripRootSlash(ref itemName);

            StringComparison stringCompareMode =
                Configuration.SCMWantCaseSensitiveItemMatch ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;

            foreach (ItemMetaData data in new List<ItemMetaData>(root.Items))
            {
                string nameMatchingSourceItemConvention = data.Name;
                FilesysHelpers.StripRootSlash(ref nameMatchingSourceItemConvention);

                // a child of the currently renamed item
                if (data is MissingItemMetaData &&
                    nameMatchingSourceItemConvention.StartsWith(itemName, stringCompareMode))
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
                string pathPrefix = MakePrefixPath(path);
                foreach (string missingPath in reportData.Missing)
                {
                    clientDeletedFiles[pathPrefix + missingPath] = missingPath;
                }
            }
            return clientDeletedFiles;
        }

        private static Dictionary<string, int> GetClientExistingFiles(string path, UpdateReportData reportData)
        {
            Dictionary<string, int> clientExistingFiles = new Dictionary<string, int>();
            if (reportData.Entries != null)
            {
                string pathPrefix = MakePrefixPath(path);
                foreach (EntryData entryData in reportData.Entries)
                {
                    clientExistingFiles[pathPrefix + entryData.path] = int.Parse(entryData.Rev);
                }
            }
            return clientExistingFiles;
        }

        private static string MakePrefixPath(string path)
        {
            string pathPrefix =
                string.IsNullOrEmpty(path) ?
                "/" :
                "/" + path + "/";
            return pathPrefix;
        }

        private bool ShouldBeIgnored(string file)
        {
            return Path.GetFileName(file) == Constants.PropFolder;
        }
    }
}
