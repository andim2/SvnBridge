using System;
using System.Collections.Generic;
using System.Text;
using CodePlex.TfsLibrary.ObjectModel;
using SvnBridge.Protocol;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Utility;
using System.IO;

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
            // is delete, and at any rate, this is a SvnBridge implementation detail
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
                ProcessDeletedItem(itemOldName, change);
                ProcessAddedOrUpdatedItem(itemNewName, change, false, false);
            }
            else
            {
                itemOldName = change.Item.RemoteName;
                itemNewName = oldItem.Name;
                ProcessAddedOrUpdatedItem(itemNewName, change, false, false);
                ProcessDeletedItem(itemOldName, change);
            }
            if (change.Item.ItemType == ItemType.Folder)
            {
                renamedItemsToBeCheckedForDeletedChildren.Add(itemNewName);
            }
        }

        private void PerformAddOrUpdate(SourceItemChange change, bool edit)
        {
            if (sourceControlProvider.IsPropertyFolder(change.Item.RemoteName))
            {
                return;
            }

            string remoteName = change.Item.RemoteName;
            bool propertyChange = false;

            if (sourceControlProvider.IsPropertyFile(change.Item.RemoteName))
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

            if (string.Equals(remoteName, _checkoutRootPath, StringComparison.InvariantCultureIgnoreCase))
            {
                ItemMetaData item = sourceControlProvider.GetItems(_targetVersion, remoteName, Recursion.None);
                if (item != null)
                {
                    _root.Properties = item.Properties;
                }
            }
            else
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

                    ItemMetaData item = folder.FindItem(itemName);
                    if (item == null ||
                         (
                            lastNamePart &&
                            item.Revision < change.Item.RemoteChangesetId &&
                            !(item is DeleteFolderMetaData) &&
                            !(item is DeleteMetaData)
                         )
                        )
                    {
                        if (item != null)
                        {
                            folder.Items.Remove(item);
                        }
                        item = sourceControlProvider.GetItems(_targetVersion, itemName, Recursion.None);
                        if (item == null)
                        {
                            // TFS will report renames even for deleted items, 
                            // since TFS reported that this was renamed, but it doesn't exists
                            // in this revision, we know it is a case of renaming a deleted file.
                            // We can safely ignore this and any of its children.
                            if (IsRenameOperation(change))
                            {
                                return;
                            }
                            if (lastNamePart && propertyChange)
                            {
                                return;
                            }
                            item = new MissingItemMetaData(itemName, _targetVersion, edit);
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
                    else if (((item is DeleteFolderMetaData) || (item is DeleteMetaData)) && IsAddOperation(change))
                    {
                        if (!propertyChange)
                        {
                            folder.Items.Remove(item);
                            item = sourceControlProvider.GetItems(change.Item.RemoteChangesetId, itemName, Recursion.None);
                            item.OriginallyDeleted = true;
                            folder.Items.Add(item);
                        }
                    }
                    if (lastNamePart == false)
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
            if (item is DeleteFolderMetaData || item is DeleteMetaData)
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
                    item = sourceControlProvider.GetItemsWithoutProperties(_targetVersion, folderName, Recursion.None);
                    if (item == null)
                    {
                        item = new DeleteFolderMetaData();
                        item.Name = folderName;
                        item.ItemRevision = _targetVersion;
                    }
                }
                folder.Items.Add(item);
            }
            else if (isLastNamePart) // we need to revert the item addition
            {
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
                    removeFolder.ItemRevision = _targetVersion;
                    folder.Items.Remove(item);
                    folder.Items.Add(removeFolder);
                }
                else if (additionForPropertyChangeOnly.ContainsKey(item) && additionForPropertyChangeOnly[item])
                {
                    ItemMetaData removeFolder = item is FolderMetaData
                                                    ? (ItemMetaData)new DeleteFolderMetaData()
                                                    : new DeleteMetaData();
                    removeFolder.Name = item.Name;
                    removeFolder.ItemRevision = _targetVersion;
                    folder.Items.Remove(item);
                    folder.Items.Add(removeFolder);
                }
                else if (item is MissingItemMetaData && ((MissingItemMetaData)item).Edit == true)
                {
                    ItemMetaData removeFolder = new DeleteMetaData();
                    removeFolder.Name = item.Name;
                    removeFolder.ItemRevision = _targetVersion;
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
    }
}
