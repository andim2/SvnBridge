using System; // StringComparison , StringSplitOptions
using System.Collections.Generic; // Dictionary , List
using CodePlex.TfsLibrary.ObjectModel; // SourceItemChange
using CodePlex.TfsLibrary.RepositoryWebSvc; // ChangeType , ItemType

namespace SvnBridge.SourceControl
{
    public class UpdateDiffEngine
    {
        private readonly TFSSourceControlProvider sourceControlProvider;
        private readonly Dictionary<string, int> clientExistingFiles;
        private readonly Dictionary<string, string> clientMissingFiles;
        private readonly List<string> renamedItemsToBeCheckedForDeletedChildren;
        private readonly Dictionary<ItemMetaData, bool> additionForPropertyChangeOnly;
        private readonly FolderMetaData _root;
        private readonly string _checkoutRootPath;
        private readonly int _targetVersion;

        public UpdateDiffEngine(FolderMetaData root,
                    string checkoutRootPath,
                    int targetVersion,
                    TFSSourceControlProvider sourceControlProvider,
                    Dictionary<string, int> clientExistingFiles,
                    Dictionary<string, string> clientMissingFiles,
                    Dictionary<ItemMetaData, bool> additionForPropertyChangeOnly,
                    List<string> renamedItemsToBeCheckedForDeletedChildren)
        {
            this._root = root;
            this._checkoutRootPath = checkoutRootPath;
            this._targetVersion = targetVersion;
            this.sourceControlProvider = sourceControlProvider;
            this.clientExistingFiles = clientExistingFiles;
            this.clientMissingFiles = clientMissingFiles;
            this.additionForPropertyChangeOnly = additionForPropertyChangeOnly;
            this.renamedItemsToBeCheckedForDeletedChildren = renamedItemsToBeCheckedForDeletedChildren;
        }

        public void Add(SourceItemChange change)
        {
            PerformAddOrUpdate(change, false);
        }

        public void Edit(SourceItemChange change)
        {
            PerformAddOrUpdate(change, true);
        }

        public void Delete(SourceItemChange change)
        {
            // we ignore it here because this only happens when the related item
            // is deleted, and at any rate, this is a SvnBridge implementation detail
            // which the client is not concerned about
            if (change.Item.RemoteName.StartsWith(Constants.PropFolder + "/") ||
                change.Item.RemoteName.EndsWith("/" + Constants.PropFolder) ||
                change.Item.RemoteName.Contains("/" + Constants.PropFolder + "/"))
            {
                return;
            }
            ProcessDeletedItem(change.Item.RemoteName, change);
        }

        public void Rename(SourceItemChange change, bool updatingForwardInTime)
        {
            ItemMetaData oldItem = sourceControlProvider.GetPreviousVersionOfItems(new SourceItem[] { change.Item }, change.Item.RemoteChangesetId)[0];

            string itemOldName;
            string itemNewName;
            if (updatingForwardInTime)
            {
                itemOldName = oldItem.Name;
                itemNewName = change.Item.RemoteName;
            }
            else
            {
                itemOldName = change.Item.RemoteName;
                itemNewName = oldItem.Name;
            }

            // svn diff output of a real Subversion server
            // always _first_ generates '-' diff for old file and _then_ '+' diff for new file
            // irrespective of whether one is doing forward- or backward-diffs,
            // and tools such as "svn up" or git-svn
            // do rely on that order being correct
            // (e.g. svn would otherwise suffer from
            // failing property queries on non-existent files),
            // thus need to always use _fixed order_ of delete/add.
            // NOTE: maybe/possibly this constraint of diff ordering
            // should be handled on UpdateReportService side only
            // and not during ItemMetaData queueing here yet,
            // in case subsequent ItemMetaData handling
            // happened to expect a different order.
            ProcessDeletedItem(itemOldName, change);
            ProcessAddedOrUpdatedItem(itemNewName, change, false, false);

            if (change.Item.ItemType == ItemType.Folder)
            {
                renamedItemsToBeCheckedForDeletedChildren.Add(itemNewName);
            }
        }

        private void PerformAddOrUpdate(SourceItemChange change, bool edit)
        {
            string remoteName = change.Item.RemoteName;

            if (sourceControlProvider.IsPropertyFolder(remoteName))
            {
                return;
            }

            bool propertyChange = false;

            if (sourceControlProvider.IsPropertyFile(remoteName))
            {
                propertyChange = true;
                remoteName = GetRemoteNameOfPropertyChange(change);
            }

            ProcessAddedOrUpdatedItem(remoteName, change, propertyChange, edit);
        }

        private string GetRemoteNameOfPropertyChange(SourceItemChange change)
        {
            string remoteName = change.Item.RemoteName;
            if (remoteName.Contains("/" + Constants.PropFolder + "/"))
            {
                if (remoteName.EndsWith("/" + Constants.PropFolder + "/" + Constants.FolderPropFile))
                {
                    remoteName = remoteName.Substring(0, remoteName.Length - ("/" + Constants.PropFolder + "/" + Constants.FolderPropFile).Length);
                }
                else
                {
                    remoteName = remoteName.Replace("/" + Constants.PropFolder + "/", "/");
                }
            }
            else if (remoteName.StartsWith(Constants.PropFolder + "/"))
            {
                if (remoteName == Constants.PropFolder + "/" + Constants.FolderPropFile)
                {
                    remoteName = "";
                }
                else
                {
                    remoteName = remoteName.Substring(Constants.PropFolder.Length + 1);
                }
            }
            return remoteName;
        }

        private static bool IsRenameOperation(SourceItemChange change)
        {
            return (change.ChangeType & ChangeType.Rename) == ChangeType.Rename;
        }

        private void ProcessAddedOrUpdatedItem(string remoteName, SourceItemChange change, bool propertyChange, bool edit)
        {
            bool alreadyInClientCurrentState = IsChangeAlreadyCurrentInClientState(ChangeType.Add,
                                                                                   remoteName,
                                                                                   change.Item.RemoteChangesetId,
                                                                                   clientExistingFiles,
                                                                                   clientMissingFiles);
            if (alreadyInClientCurrentState)
            {
                return;
            }

            // Special case for changes of source root item (why? performance opt?):
            if (ItemMetaData.IsSamePath(remoteName, _checkoutRootPath))
            {
                ItemMetaData item = sourceControlProvider.GetItems(_targetVersion, remoteName, Recursion.None);
                if (item != null)
                {
                    _root.Properties = item.Properties;
                }
            }
            else // standard case (other items)
            {
                FolderMetaData folder = _root;
                string itemName = _checkoutRootPath;
                string[] nameParts;
                if (_checkoutRootPath != "")
                    nameParts = remoteName.Substring(_checkoutRootPath.Length + 1).Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                else
                    nameParts = remoteName.Split('/');

                for (int i = 0; i < nameParts.Length; i++)
                {
                    bool lastNamePart = false;
                    if (i == nameParts.Length - 1)
                        lastNamePart = true;

                    if (itemName != "" && !itemName.EndsWith("/"))
                        itemName += "/" + nameParts[i];
                    else
                        itemName += nameParts[i];

                    // Detect our possibly pre-existing record of this item within the changeset version range
                    // that we're in the process of analyzing/collecting...
                    // This existing item may possibly be a placeholder (stub folder).
                    ItemMetaData item = folder.FindItem(itemName);
                    if (item == null ||
                         (
                            lastNamePart &&
                            item.Revision < change.Item.RemoteChangesetId &&
                            !IsDeleteMetaDataKind(item)
                         )
                        )
                    {
                        // ...then remove this prior item...
                        if (item != null)
                        {
                            folder.Items.Remove(item);
                        }
                        // ...and fetch the updated one
                        // for the currently processed version:
                        var processedVersion = _targetVersion;
                        item = sourceControlProvider.GetItems(processedVersion, itemName, Recursion.None);
                        if (item == null)
                        {
                            // THIS IS *NOT* TRUE!!
                            // A subsequently executed delete operation handler depends on being able to discover a previously added item
                            // in order to have it *cleanly* removed (reverted!) into oblivion
                            // rather than queueing a (bogus) *active* DeleteItemMetaData indication.
                            // If there's no item remaining to revert against,
                            // this means the bogus delete operation will remain standing,
                            // which is a fatal error
                            // when sending an SVN delete op to the client
                            // which then complains
                            // that *within this particular changeset update range* (for incremental updates!)
                            // the file did not actually go from existence to non-existence
                            // (--> "is not under version control").
                            // Or, in other words,
                            // we simply CANNOT TAKE ANY SHORTCUTS WITHIN THE FULLY INCREMENTAL HANDLING OF CHANGESET UPDATES,
                            // each step needs to be properly accounted for
                            // in order for subsequent steps to be able to draw their conclusions from prior state!
                            // Adding dirty final "post-processing" to get rid of improper delete-only entries
                            // (given an actually pre-non-existing item at client's start version!)
                            // would be much less desirable
                            // than having all incremental change recordings
                            // resolve each other
                            // in a nicely fully complementary manner.
#if false
                            if (IsRenameOperation(change))
                            {
                                // TFS will report renames even for deleted items -
                                // since TFS reported above that this was renamed,
                                // but it doesn't exist in this revision,
                                // we know it is a case of renaming a deleted file.
                                // We can safely ignore this and any of its children.
                                return;
                            }
#endif
                            if (lastNamePart && propertyChange)
                            {
                                return;
                            }
                            item = new MissingItemMetaData(itemName, processedVersion, edit);
                        }
                        if (!lastNamePart)
                        {
                            StubFolderMetaData stubFolder = new StubFolderMetaData();
                            stubFolder.RealFolder = (FolderMetaData)item;
                            stubFolder.Name = item.Name;
                            stubFolder.ItemRevision = item.ItemRevision;
                            stubFolder.PropertyRevision = item.PropertyRevision;
                            stubFolder.LastModifiedDate = item.LastModifiedDate;
                            stubFolder.Author = item.Author;
                            item = stubFolder;
                        }
                        folder.Items.Add(item);
                        SetAdditionForPropertyChangeOnly(item, propertyChange);
                    }
                    else if ((item is StubFolderMetaData) && lastNamePart)
                    {
                        folder.Items.Remove(item);
                        folder.Items.Add(((StubFolderMetaData)item).RealFolder);
                    }
                    else if (IsDeleteMetaDataKind(item))
                    { // former item was a DELETE...

                        // ...and new one then _resurrects_ the (_actually_ deleted) item:
                        if (IsAddOperation(change))
                        {
                          if (!propertyChange)
                          {
                              folder.Items.Remove(item);
                              item = sourceControlProvider.GetItems(change.Item.RemoteChangesetId, itemName, Recursion.None);
                              item.OriginallyDeleted = true;
                              folder.Items.Add(item);
                          }
                        }
                        // ...or _renames_ the (pseudo-deleted) item!
                        // (OBEY VERY SPECIAL CASE: _similar-name_ rename (EXISTING ITEM LOOKUP SUCCESSFUL ABOVE!!), i.e. filename-case-only change)
                        else if (IsRenameOperation(change))
                        {
                          // Such TFS-side renames need to be reflected
                          // as a SVN delete/add (achieve rename *with* history!) operation,
                          // thus definitely *append* an ADD op to the *existing* DELETE op.
                          // [Indeed, for different-name renames,
                          // upon "svn diff" requests
                          // SvnBridge does generate both delete and add diffs,
                          // whereas for similar-name renames it previously did not -> buggy!]
                          item = sourceControlProvider.GetItems(change.Item.RemoteChangesetId, itemName, Recursion.None);
                          folder.Items.Add(item);
                        }
                    }
                    if (lastNamePart == false) // this conditional merely required to prevent cast of non-FolderMetaData-type objects below :(
                    {
                        folder = (FolderMetaData)item;
                    }
                }
            }
        }

        private static bool IsAddOperation(SourceItemChange change)
        {
            return ((change.ChangeType & ChangeType.Add) == ChangeType.Add) ||
                   ((change.ChangeType & ChangeType.Branch) == ChangeType.Branch) ||
                   ((change.ChangeType & ChangeType.Undelete) == ChangeType.Undelete);
        }

        private void SetAdditionForPropertyChangeOnly(ItemMetaData item, bool propertyChange)
        {
            if (item == null)
                return;
            if (propertyChange == false)
            {
                additionForPropertyChangeOnly[item] = propertyChange;
            }
            else
            {
                if (additionForPropertyChangeOnly.ContainsKey(item) == false)
                    additionForPropertyChangeOnly[item] = propertyChange;
            }
        }

        private void ProcessDeletedItem(string remoteName, SourceItemChange change)
        {
            bool alreadyChangedInCurrentClientState = IsChangeAlreadyCurrentInClientState(ChangeType.Delete,
                                                                                          remoteName,
                                                                                          change.Item.RemoteChangesetId,
                                                                                          clientExistingFiles,
                                                                                          clientMissingFiles);
            if (alreadyChangedInCurrentClientState)
            {
                RemoveMissingItem(remoteName, _root);
                return;
            }

            string folderName = _checkoutRootPath;
            string remoteNameStart = remoteName.StartsWith(_checkoutRootPath) ? _checkoutRootPath : folderName;

            string[] nameParts = remoteName.Substring(remoteNameStart.Length)
                .Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            FolderMetaData folder = _root;
            for (int i = 0; i < nameParts.Length; i++)
            {
                bool isLastNamePart = i == nameParts.Length - 1;

                if (folderName != "" && !folderName.EndsWith("/"))
                    folderName += "/" + nameParts[i];
                else
                    folderName += nameParts[i];

                bool fullyHandled = HandleDeleteItem(remoteName, change, folderName, ref folder, isLastNamePart);
                if (fullyHandled)
                    break;
            }
            if (nameParts.Length == 0)//we have to delete the checkout root itself
            {
                HandleDeleteItem(remoteName, change, folderName, ref folder, true);
            }
        }

        private bool HandleDeleteItem(string remoteName, SourceItemChange change, string folderName, ref FolderMetaData folder, bool isLastNamePart)
        {
            ItemMetaData item = folder.FindItem(folderName);
            // Shortcut: valid item in our cache, and it's a delete already? We're done :)
            if (IsDeleteMetaDataKind(item))
                return true;

            if (item == null)
            {
                if (isLastNamePart)
                {
                    if (change.Item.ItemType == ItemType.File)
                        item = new DeleteMetaData();
                    else
                        item = new DeleteFolderMetaData();

                    item.Name = remoteName;
                    item.ItemRevision = change.Item.RemoteChangesetId;
                }
                else
                {
                    var processedVersion = _targetVersion;
                    item = sourceControlProvider.GetItemsWithoutProperties(processedVersion, folderName, Recursion.None);
                    if (item == null)
                    {
                        item = new DeleteFolderMetaData();
                        item.Name = folderName;
                        item.ItemRevision = processedVersion;
                    }
                }
                folder.Items.Add(item);
            }
            else if (isLastNamePart) // we need to revert the item addition
            {
                var processedVersion = _targetVersion;
                if (item.OriginallyDeleted) // convert back into a delete
                {
                    folder.Items.Remove(item);
                    if (change.Item.ItemType == ItemType.File)
                        item = new DeleteMetaData();
                    else
                        item = new DeleteFolderMetaData();

                    item.Name = remoteName;
                    item.ItemRevision = change.Item.RemoteChangesetId;
                    folder.Items.Add(item);
                }
                else if (item is StubFolderMetaData)
                {
                    DeleteFolderMetaData removeFolder = new DeleteFolderMetaData();
                    removeFolder.Name = item.Name;
                    removeFolder.ItemRevision = processedVersion;
                    folder.Items.Remove(item);
                    folder.Items.Add(removeFolder);
                }
                else if (additionForPropertyChangeOnly.ContainsKey(item) && additionForPropertyChangeOnly[item])
                {
                    ItemMetaData removeFolder = item is FolderMetaData
                                                    ? (ItemMetaData)new DeleteFolderMetaData()
                                                    : new DeleteMetaData();
                    removeFolder.Name = item.Name;
                    removeFolder.ItemRevision = processedVersion;
                    folder.Items.Remove(item);
                    folder.Items.Add(removeFolder);
                }
                else if (item is MissingItemMetaData && ((MissingItemMetaData)item).Edit == true)
                {
                    ItemMetaData removeFolder = new DeleteMetaData();
                    removeFolder.Name = item.Name;
                    removeFolder.ItemRevision = processedVersion;
                    folder.Items.Remove(item);
                    folder.Items.Add(removeFolder);
                }
                else
                {
                    folder.Items.Remove(item);
                }
            }
            folder = (item as FolderMetaData) ?? folder;
            return false;
        }

        private static bool IsChangeAlreadyCurrentInClientState(ChangeType changeType,
                                                                string itemPath,
                                                                int itemRevision,
                                                                IDictionary<string, int> clientExistingFiles,
                                                                IDictionary<string, string> clientDeletedFiles)
        {
            string changePath = itemPath;
            if (changePath.StartsWith("/") == false)
                changePath = "/" + changePath;
            if (((changeType & ChangeType.Add) == ChangeType.Add) ||
                ((changeType & ChangeType.Edit) == ChangeType.Edit))
            {
                if ((clientExistingFiles.ContainsKey(changePath)) && (clientExistingFiles[changePath] >= itemRevision))
                {
                    return true;
                }

                foreach (string clientExistingFile in clientExistingFiles.Keys)
                {
                    if (changePath.StartsWith(clientExistingFile + "/") &&
                        (clientExistingFiles[clientExistingFile] >= itemRevision))
                    {
                        return true;
                    }
                }
            }
            else if ((changeType & ChangeType.Delete) == ChangeType.Delete)
            {
                if (clientDeletedFiles.ContainsKey(changePath) ||
                    (clientExistingFiles.ContainsKey(changePath) && (clientExistingFiles[changePath] >= itemRevision)))
                {
                    return true;
                }

                foreach (string clientDeletedFile in clientDeletedFiles.Keys)
                {
                    if (changePath.StartsWith(clientDeletedFile + "/"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool RemoveMissingItem(string name, FolderMetaData folder)
        {
            foreach (ItemMetaData item in folder.Items)
            {
                if (item.Name == name && item is MissingItemMetaData)
                {
                    folder.Items.Remove(item);
                    return true;
                }
                FolderMetaData subFolder = item as FolderMetaData;
                if (subFolder != null)
                {
                    if (RemoveMissingItem(name, subFolder))
                        return true;
                }
            }
            return false;
        }

        private static bool IsDeleteMetaDataKind(ItemMetaData item)
        {
          return (item is DeleteFolderMetaData || item is DeleteMetaData);
        }
    }
}
