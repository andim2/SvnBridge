using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ClientEngine
{
    public partial class TfsEngine
    {
        public void Syncup(string directory,
                           AddSourceItemCallback addItemCallback,
                           SyncupCallback syncupCallback)
        {
            Guard.ArgumentNotNullOrEmpty(directory, "localPath");

            if (!fileSystem.DirectoryExists(directory))
                _Callback(syncupCallback, directory, SourceItemResult.E_DirectoryNotFound);
            else if (!tfsState.IsFolderTracked(directory))
                _Callback(syncupCallback, directory, SourceItemResult.E_NotUnderSourceControl);
            else
            {
                ValidateDirectoryStructure(directory);

                Syncup_Delete(directory, syncupCallback);
                Syncup_Update(directory, syncupCallback);
                Syncup_Add(directory, addItemCallback, syncupCallback);
            }
        }

        void Syncup_Add(string directory,
                        AddSourceItemCallback addItemCallback,
                        SyncupCallback syncupCallback)
        {
            foreach (SourceItem sourceItem in tfsState.GetSourceItems(directory))
                if (sourceItem.ItemType == ItemType.File)
                    Syncup_Add_File(sourceItem, addItemCallback, syncupCallback);
                else
                    Syncup_Add_Folder(sourceItem, addItemCallback, syncupCallback);
        }

        void Syncup_Add_File(SourceItem sourceItem,
                             AddSourceItemCallback addItemCallback,
                             SyncupCallback syncupCallback)
        {
            if (sourceItem.LocalItemStatus == SourceItemStatus.Unversioned)
                if (!IsIgnored(sourceItem.LocalName, sourceItem.ItemType) && (addItemCallback == null || addItemCallback(sourceItem)))
                {
                    Add_File_Helper(sourceItem.LocalName,
                                    delegate(SourceItem item,
                                             SourceItemResult result)
                                    {
                                        _Callback(syncupCallback, item, SyncupAction.LocalAdded, result);
                                    }, true);
                }
        }

        void Syncup_Add_Folder(SourceItem sourceItem,
                               AddSourceItemCallback addItemCallback,
                               SyncupCallback syncupCallback)
        {
            switch (sourceItem.LocalItemStatus)
            {
                case SourceItemStatus.Unversioned:
                    if (!IsIgnored(sourceItem.LocalName, sourceItem.ItemType) && (addItemCallback == null || addItemCallback(sourceItem)))
                    {
                        Add_Folder_Helper(sourceItem.LocalName, false,
                                          delegate(SourceItem item,
                                                   SourceItemResult result)
                                          {
                                              _Callback(syncupCallback, item, SyncupAction.LocalAdded, result);
                                          }, true);

                        Syncup_Add(sourceItem.LocalName, addItemCallback, syncupCallback);
                    }
                    break;

                case SourceItemStatus.Add:
                case SourceItemStatus.Unmodified:
                    Syncup_Add(sourceItem.LocalName, addItemCallback, syncupCallback);
                    break;
            }
        }

        void Syncup_Delete(string directory,
                           SyncupCallback syncupCallback)
        {
            foreach (SourceItem sourceItem in tfsState.GetSourceItems(directory))
                if (sourceItem.ItemType == ItemType.File)
                    Syncup_Delete_File(sourceItem, syncupCallback);
                else
                    Syncup_Delete_Folder(sourceItem, syncupCallback);
        }

        void Syncup_Delete_File(SourceItem sourceItem,
                                SyncupCallback syncupCallback)
        {
            if (sourceItem.LocalItemStatus == SourceItemStatus.Missing)
            {
                if (sourceItem.OriginalLocalItemStatus == SourceItemStatus.Add)
                    Revert_File(sourceItem.LocalName,
                                delegate(SourceItem item,
                                         SourceItemResult result)
                                {
                                    _Callback(syncupCallback, item, SyncupAction.LocalReverted, result);
                                });
                else
                    Delete_File_Helper(sourceItem.LocalName, true,
                                       delegate(SourceItem item,
                                                SourceItemResult result)
                                       {
                                           _Callback(syncupCallback, item, Syncup_DeleteActionFromResult(result), result);
                                       });
            }
        }

        void Syncup_Delete_Folder(SourceItem sourceItem,
                                  SyncupCallback syncupCallback)
        {
            switch (sourceItem.LocalItemStatus)
            {
                case SourceItemStatus.Add:
                case SourceItemStatus.Unmodified:
                    Syncup_Delete(sourceItem.LocalName, syncupCallback);
                    break;

                case SourceItemStatus.Missing:
                    Delete_Folder_Helper(sourceItem.LocalName, true,
                                         delegate(SourceItem item,
                                                  SourceItemResult result)
                                         {
                                             _Callback(syncupCallback, item, Syncup_DeleteActionFromResult(result), result);
                                         });
                    break;
            }
        }

        static SyncupAction Syncup_DeleteActionFromResult(SourceItemResult result)
        {
            switch (result)
            {
                case SourceItemResult.S_Ok:
                case SourceItemResult.S_ForcedDelete:
                    return SyncupAction.LocalDeleted;

                default:
                    return SyncupAction.None;
            }
        }

        void Syncup_Update(string directory,
                           SyncupCallback callback)
        {
            Update(directory, true, VersionSpec.Latest,
                   delegate(SourceItem item,
                            UpdateAction action,
                            SourceItemResult result)
                   {
                       _Callback(callback, item, (SyncupAction)action, result);
                   });
        }

        // Callback helpers

        static void _Callback(SyncupCallback callback,
                              string localPath,
                              SourceItemResult result)
        {
            if (callback != null)
                callback(SourceItem.FromLocalPath(localPath), SyncupAction.None, result);
        }

        static void _Callback(SyncupCallback callback,
                              SourceItem item,
                              SyncupAction action,
                              SourceItemResult result)
        {
            if (callback != null)
                callback(item, action, result);
        }
    }
}