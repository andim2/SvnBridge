using System;
using System.Collections.Generic;
using System.IO;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        static void CombineLocalAndServerItem(SourceItem localItem,
                                              SourceItem serverItem)
        {
            localItem.ItemId = serverItem.ItemId;
            localItem.DownloadUrl = serverItem.DownloadUrl;
            localItem.RemoteChangesetId = serverItem.RemoteChangesetId;
            localItem.RemoteDate = serverItem.RemoteDate;
            localItem.RemoteName = serverItem.RemoteName;
            localItem.RemoteSize = serverItem.RemoteSize;

            if (localItem.LocalChangesetId == 0)
                localItem.RemoteItemStatus = SourceItemStatus.Add;
            else if (localItem.LocalChangesetId == localItem.RemoteChangesetId)
                localItem.RemoteItemStatus = SourceItemStatus.Unmodified;
            else
                localItem.RemoteItemStatus = SourceItemStatus.Modified;
        }

        List<SourceItem> GetCombinedItems(List<SourceItem> serverItems,
                                          string directory,
                                          string serverPath,
                                          bool recursive)
        {
            List<SourceItem> items = new List<SourceItem>();
            items.Add(GetSourceItem(directory));
            GetLocalItems(items, directory, recursive);

            foreach (SourceItem localItem in items)
            {
                string serverItemPath = TfsUtil.LocalPathToServerPath(serverPath,
                                                                      directory,
                                                                      localItem.LocalName,
                                                                      localItem.ItemType);

                SourceItem serverItem = serverItems.Find(delegate(SourceItem item)
                                                         {
                                                             return string.Compare(serverItemPath, item.RemoteName, true) == 0;
                                                         });

                if (serverItem != null)
                {
                    CombineLocalAndServerItem(localItem, serverItem);
                    serverItems.Remove(serverItem);
                }
                else
                {
                    switch (localItem.LocalItemStatus)
                    {
                        case SourceItemStatus.Add:
                        case SourceItemStatus.Unversioned:
                            localItem.RemoteItemStatus = SourceItemStatus.None;
                            break;

                        default:
                            localItem.RemoteItemStatus = SourceItemStatus.Delete;
                            break;
                    }
                }
            }

            foreach (SourceItem serverItem in serverItems)
            {
                serverItem.LocalName = fileSystem.GetFullPath(fileSystem.CombinePath(directory,
                                                                                     serverItem.RemoteName.Substring(serverPath.Length - 1).TrimStart('/')));
                serverItem.RemoteItemStatus = SourceItemStatus.Add;
            }

            items.AddRange(serverItems);
            items.Sort();
            return items;
        }

        SourceItem GetCombinedSourceItem(string filename,
                                         VersionSpec version)
        {
            string parentDirectory = fileSystem.GetDirectoryName(filename);
            TfsFolderInfo folderInfo = tfsState.GetFolderInfo(parentDirectory);
            string serverItemPath = TfsUtil.LocalPathToServerPath(folderInfo.ServerPath,
                                                                  parentDirectory,
                                                                  filename,
                                                                  ItemType.File);

            SourceItem localItem = GetSourceItem(filename);
            SourceItem[] serverItems = QueryItems(folderInfo.TfsUrl, serverItemPath, RecursionType.None, version);

            if (serverItems != null && serverItems.Length > 0)
                CombineLocalAndServerItem(localItem, serverItems[0]);

            return localItem;
        }

        List<SourceItem> GetCombinedSourceItems(string directory,
                                                VersionSpec version,
                                                bool recursive)
        {
            Guard.ArgumentNotNullOrEmpty(directory, "directory");
            Guard.ArgumentNotNull(version, "version");

            if (!fileSystem.DirectoryExists(directory))
                throw new DirectoryNotFoundException();

            TfsFolderInfo folderInfo = tfsState.GetFolderInfo(directory);

            if (folderInfo == null)
                throw new ArgumentException(directory + " is not under source control", "directory");

            List<SourceItem> serverItems = new List<SourceItem>(QueryItems(folderInfo.TfsUrl,
                                                                           folderInfo.ServerPath,
                                                                           recursive ? RecursionType.Full : RecursionType.OneLevel,
                                                                           version));

            return GetCombinedItems(serverItems, directory, folderInfo.ServerPath, recursive);
        }

        void GetLocalItems(List<SourceItem> items,
                           string directory,
                           bool recursive)
        {
            SourceItem[] localItems = tfsState.GetSourceItems(directory);
            items.AddRange(localItems);

            if (recursive)
                foreach (SourceItem item in localItems)
                    if (item.ItemType == ItemType.Folder && item.LocalItemStatus != SourceItemStatus.Unversioned)
                        GetLocalItems(items, item.LocalName, recursive);
        }

        public void Status(string localPath,
                           VersionSpec version,
                           bool recursive,
                           bool connectToServer,
                           SourceItemCallback callback)
        {
            Guard.ArgumentNotNullOrEmpty(localPath, "localPath");
            Guard.ArgumentNotNull(version, "version");
            Guard.ArgumentNotNull(callback, "callback");

            if (connectToServer)
                StatusWithServer(localPath, version, recursive, callback);
            else
                StatusLocalOnly(localPath, recursive, callback);
        }

        protected virtual void StatusLocalOnly(string localPath,
                                               bool recursive,
                                               SourceItemCallback callback)
        {
            if (tfsState.IsFileTracked(localPath) || fileSystem.FileExists(localPath))
                StatusLocalOnly_File(localPath, callback);
            else if (tfsState.IsFolderTracked(localPath) || fileSystem.DirectoryExists(localPath))
                StatusLocalOnly_Folder(localPath, recursive, callback);
            else
                _Callback(callback, localPath, SourceItemResult.E_PathNotFound);
        }

        void StatusLocalOnly_File(string filename,
                                  SourceItemCallback callback)
        {
            if (!IsFolderTracked(fileSystem.GetDirectoryName(filename)))
                _Callback(callback, filename, SourceItemResult.E_NotInAWorkingFolder);
            else
                _Callback(callback, tfsState.GetSourceItem(filename), SourceItemResult.S_Ok);
        }

        void StatusLocalOnly_Folder(string directory,
                                    bool recursive,
                                    SourceItemCallback callback)
        {
            if (!IsFolderTracked(directory))
                _Callback(callback, directory, SourceItemResult.E_NotInAWorkingFolder);
            else
            {
                ValidateDirectoryStructure(directory);

                foreach (SourceItem item in tfsState.GetSourceItems(directory))
                {
                    if (item.LocalItemStatus != SourceItemStatus.Unversioned || !IsIgnored(item.LocalName, item.ItemType))
                        _Callback(callback, item);

                    if (recursive && item.ItemType == ItemType.Folder && (item.LocalItemStatus == SourceItemStatus.Add || item.LocalItemStatus == SourceItemStatus.Unmodified))
                        StatusLocalOnly_Folder(fileSystem.CombinePath(directory, item.LocalName), recursive, callback);
                }
            }
        }

        protected virtual void StatusWithServer(string localPath,
                                                VersionSpec version,
                                                bool recursive,
                                                SourceItemCallback callback)
        {
            if (tfsState.IsFileTracked(localPath) || fileSystem.FileExists(localPath))
                StatusWithServer_File(localPath, callback);
            else if (tfsState.IsFolderTracked(localPath) || fileSystem.DirectoryExists(localPath))
                StatusWithServer_Folder(localPath, recursive, callback);
            else
                _Callback(callback, localPath, SourceItemResult.E_PathNotFound);
        }

        void StatusWithServer_File(string filename,
                                   SourceItemCallback callback)
        {
            if (!IsParentDirectoryTracked(filename))
            {
                _Callback(callback, filename, SourceItemResult.E_NotInAWorkingFolder);
                return;
            }

            ValidateDirectoryStructure(fileSystem.GetDirectoryName(filename));

            SourceItem item = GetCombinedSourceItem(filename, VersionSpec.Latest);

            if (!(item.LocalItemStatus == SourceItemStatus.Unversioned &&
                  item.RemoteItemStatus == SourceItemStatus.None &&
                  IsIgnored(item.LocalName, item.ItemType)))
                _Callback(callback, item, SourceItemResult.S_Ok);
        }

        void StatusWithServer_Folder(string directory,
                                     bool recursive,
                                     SourceItemCallback callback)
        {
            if (!tfsState.IsFolderTracked(directory))
            {
                if (IsParentDirectoryTracked(directory))
                    _Callback(callback, SourceItem.FromLocalPath(directory));
                else
                    _Callback(callback, directory, SourceItemResult.E_NotInAWorkingFolder);

                return;
            }

            ValidateDirectoryStructure(directory);

            foreach (SourceItem i in GetCombinedSourceItems(directory, VersionSpec.Latest, recursive))
                if (!(i.LocalItemStatus == SourceItemStatus.Unversioned &&
                      i.RemoteItemStatus == SourceItemStatus.None &&
                      IsIgnored(i.LocalName, i.ItemType)))
                    _Callback(callback, i);
        }
    }
}