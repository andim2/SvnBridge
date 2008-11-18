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
        private readonly IDictionary<ItemMetaData, bool> additionForPropertyChangeOnly;
        private readonly List<string> renamedItemsToBeCheckedForDeletedChildren;

        public UpdateDiffEngine(TFSSourceControlProvider sourceControlProvider,
                    Dictionary<string, int> clientExistingFiles,
                    Dictionary<string, string> clientMissingFiles,
                    IDictionary<ItemMetaData, bool> additionForPropertyChangeOnly,
                    List<string> renamedItemsToBeCheckedForDeletedChildren)
        {
            this.sourceControlProvider = sourceControlProvider;
            this.clientExistingFiles = clientExistingFiles;
            this.clientMissingFiles = clientMissingFiles;
            this.additionForPropertyChangeOnly = additionForPropertyChangeOnly;
            this.renamedItemsToBeCheckedForDeletedChildren = renamedItemsToBeCheckedForDeletedChildren;
        }

        public void PerformAddOrUpdate(int targetVersion, string checkoutRootPath, SourceItemChange change, FolderMetaData root, bool edit)
        {
            if (change.Item.RemoteName.EndsWith("/" + Constants.PropFolder))
            {
                return;
            }
            ItemInformation itemInformation = GetItemInformation(change);

            ProcessAddedOrUpdatedItem(checkoutRootPath,
                             itemInformation.RemoteName,
                             change,
                             itemInformation.PropertyChange,
                             root,
                             targetVersion,
                             edit);
        }

        public void PerformDelete(int targetVersion, string checkoutRootPath, SourceItemChange change, FolderMetaData root)
        {
            // we ignore it here because this only happens when the related item
            // is delete, and at any rate, this is a SvnBridge implementation detail
            // which the client is not concerned about
            if (change.Item.RemoteName.EndsWith("/" + Constants.PropFolder) ||
                change.Item.RemoteName.Contains("/" + Constants.PropFolder + "/"))
            {
                return;
            }
            ProcessDeletedItem(checkoutRootPath, change.Item.RemoteName, change, root, targetVersion);
        }

        public void PerformRename(int targetVersion, string checkoutRootPath, SourceItemChange change, FolderMetaData root, bool updatingForwardInTime)
        {
            ItemMetaData oldItem =
                sourceControlProvider.GetPreviousVersionOfItems(new SourceItem[] { change.Item }, change.Item.RemoteChangesetId)[0];

            if (updatingForwardInTime)
            {
                ProcessDeletedItem(checkoutRootPath,
                                   oldItem.Name,
                                   change,
                                   root,
                                   targetVersion);
                ProcessAddedItem(checkoutRootPath,
                                 change.Item.RemoteName,
                                 change,
                                 false,
                                 root,
                                 targetVersion);
            }
            else
            {
                ProcessAddedItem(checkoutRootPath,
                                 oldItem.Name,
                                 change,
                                 false,
                                 root,
                                 targetVersion);

                ProcessDeletedItem(checkoutRootPath,
                                   change.Item.RemoteName,
                                   change,
                                   root,
                                   targetVersion);
            }
            if (change.Item.ItemType == ItemType.Folder)
            {
                string itemName = updatingForwardInTime ? change.Item.RemoteName : oldItem.Name;
                renamedItemsToBeCheckedForDeletedChildren.Add(itemName);
            }
        }

        private static ItemInformation GetItemInformation(SourceItemChange change)
        {
            string remoteName = change.Item.RemoteName;
            bool propertyChange = false;
            if (remoteName.Contains("/" + Constants.PropFolder + "/"))
            {
                propertyChange = true;
                if (remoteName.EndsWith("/" + Constants.PropFolder + "/" + Constants.FolderPropFile))
                {
                    remoteName =
                        remoteName.Substring(0,
                                             remoteName.IndexOf("/" + Constants.PropFolder + "/" +
                                                                Constants.FolderPropFile));
                }
                else
                {
                    remoteName = remoteName.Replace("/" + Constants.PropFolder + "/", "/");
                }
            }
            else if (remoteName.StartsWith(Constants.PropFolder + "/"))
            {
                propertyChange = true;
                if (remoteName == Constants.PropFolder + "/" + Constants.FolderPropFile)
                {
                    remoteName = "";
                }
                else
                {
                    remoteName = remoteName.Substring(Constants.PropFolder.Length + 1);
                }
            }
            return new ItemInformation(propertyChange, remoteName);
        }

        private static bool IsRenameOperation(SourceItemChange change)
        {
            return (change.ChangeType & ChangeType.Rename) == ChangeType.Rename;
        }

        private void ProcessAddedItem(string checkoutRootPath,
                                      string remoteName,
                                      SourceItemChange change,
                                      bool propertyChange,
                                      FolderMetaData root,
                                      int targetVersion)
        {
            ProcessAddedOrUpdatedItem(checkoutRootPath, remoteName, change, propertyChange, root, targetVersion, false);
        }

        private void ProcessAddedOrUpdatedItem(string checkoutRootPath,
                                      string remoteName,
                                      SourceItemChange change,
                                      bool propertyChange,
                                      FolderMetaData root,
                                      int targetVersion,
                                      bool edit)
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

            if (string.Equals(remoteName, checkoutRootPath, StringComparison.InvariantCultureIgnoreCase))
            {
                ItemMetaData item = sourceControlProvider.GetItems(targetVersion, remoteName, Recursion.None);
                if (item != null)
                {
                    root.Properties = item.Properties;
                }
            }
            else
            {
                FolderMetaData folder = root;
                string itemName = checkoutRootPath;
                if (itemName.StartsWith("/") == false)
                    itemName = "/" + itemName;
                string[] nameParts;
                if (checkoutRootPath != "")
                    nameParts = remoteName.Substring(checkoutRootPath.Length + 1).Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                else
                    nameParts = remoteName.Split('/');

                for (int i = 0; i < nameParts.Length; i++)
                {
                    bool lastNamePart = false;
                    if (i == nameParts.Length - 1)
                        lastNamePart = true;

                    if (!itemName.EndsWith("/"))
                        itemName += "/" + nameParts[i];
                    else
                        itemName += nameParts[i];

                    ItemMetaData item = folder.FindItem(itemName);
                    if (item == null)
                    {
                        item = sourceControlProvider.GetItems(targetVersion, itemName, Recursion.None);
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
                            item = new MissingItemMetaData(itemName, targetVersion, edit);
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
                    else if ((item is DeleteFolderMetaData) && !lastNamePart)
                    {
                        return;
                    }
                    else if (((item is DeleteFolderMetaData) || (item is DeleteMetaData)) &&
                             ((change.ChangeType & ChangeType.Add) == ChangeType.Add))
                    {
                        if (!propertyChange)
                        {
                            folder.Items.Remove(item);
                        }
                    }
                    if (lastNamePart == false)
                    {
                        folder = (FolderMetaData)item;
                    }
                }
            }
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

        private void ProcessDeletedItem(string checkoutRootPath,
                                        string remoteName,
                                        SourceItemChange change,
                                        FolderMetaData root,
                                        int targetVersion)
        {
            bool alreadyChangedInCurrentClientState = IsChangeAlreadyCurrentInClientState(ChangeType.Delete,
                                                                                          remoteName,
                                                                                          change.Item.RemoteChangesetId,
                                                                                          clientExistingFiles,
                                                                                          clientMissingFiles);
            if (alreadyChangedInCurrentClientState)
            {
                root.RemoveMissingItem(remoteName);
                return;
            }

            string folderName = checkoutRootPath;
            if (folderName.StartsWith("/") == false)
                folderName = "/" + folderName;
            string remoteNameStart = remoteName.StartsWith(checkoutRootPath) ? checkoutRootPath : folderName;

            string[] nameParts = remoteName.Substring(remoteNameStart.Length)
                .Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            FolderMetaData folder = root;
            for (int i = 0; i < nameParts.Length; i++)
            {
                bool isLastNamePart = i == nameParts.Length - 1;

                if (!folderName.EndsWith("/"))
                    folderName += "/" + nameParts[i];
                else
                    folderName += nameParts[i];

                HandleDeleteItem(remoteName, change, folderName, ref folder, isLastNamePart, targetVersion);
            }
            if (nameParts.Length == 0)//we have to delete the checkout root itself
            {
                HandleDeleteItem(remoteName, change, folderName, ref folder, true, targetVersion);
            }
        }

        private void HandleDeleteItem(string remoteName, SourceItemChange change, string folderName,
                                      ref FolderMetaData folder, bool isLastNamePart, int targetVersion)
        {
            if (folderName.StartsWith("/") == false)
                folderName = "/" + folderName;
            ItemMetaData item = folder.FindItem(folderName);
            if (item is DeleteFolderMetaData)
                return;

            if (item == null)
            {
                if (isLastNamePart)
                {
                    if (change.Item.ItemType == ItemType.File)
                    {
                        item = new DeleteMetaData();
                    }
                    else
                    {
                        item = new DeleteFolderMetaData();
                    }

                    item.Name = remoteName;
                    item.ItemRevision = change.Item.RemoteChangesetId;
                }
                else
                {
                    item = sourceControlProvider.GetItemsWithoutProperties(targetVersion, folderName, Recursion.None);
                    if (item == null)
                    {
                        item = new DeleteFolderMetaData();
                        item.Name = folderName;
                        item.ItemRevision = targetVersion;
                    }
                }
                folder.Items.Add(item);
            }
            else if (isLastNamePart) // we need to revert the item addition
            {
                if (item is StubFolderMetaData)
                {
                    DeleteFolderMetaData removeFolder = new DeleteFolderMetaData();
                    removeFolder.Name = item.Name;
                    removeFolder.ItemRevision = targetVersion;
                    folder.Items.Remove(item);
                    folder.Items.Add(removeFolder);
                }
                else if (additionForPropertyChangeOnly.ContainsKey(item) && additionForPropertyChangeOnly[item])
                {
                    ItemMetaData removeFolder = item is FolderMetaData
                                                    ? (ItemMetaData)new DeleteFolderMetaData()
                                                    : new DeleteMetaData();
                    removeFolder.Name = item.Name;
                    removeFolder.ItemRevision = targetVersion;
                    folder.Items.Remove(item);
                    folder.Items.Add(removeFolder);
                }
                else if (item is MissingItemMetaData && ((MissingItemMetaData)item).Edit == true)
                {
                    ItemMetaData removeFolder = new DeleteMetaData();
                    removeFolder.Name = item.Name.Substring(1);
                    removeFolder.ItemRevision = targetVersion;
                    folder.Items.Remove(item);
                    folder.Items.Add(removeFolder);
                }
                else
                {
                    folder.Items.Remove(item);
                }
            }
            folder = (item as FolderMetaData) ?? folder;
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

        #region Nested type: ItemInformation

        private class ItemInformation
        {
            public readonly bool PropertyChange;
            public readonly string RemoteName;

            public ItemInformation(bool propertyChange,
                                   string remoteName)
            {
                PropertyChange = propertyChange;
                RemoteName = remoteName;
            }
        }

        #endregion
    }
}
