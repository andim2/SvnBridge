using System; // IntPtr.Size
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.SourceControl;
using SvnBridge.Utility; // Helper.CooperativeSleep()

namespace SvnBridge.Infrastructure
{
    public class AsyncItemLoader
    {
        private readonly FolderMetaData folderInfo;
        private readonly TFSSourceControlProvider sourceControlProvider;
        private readonly long cacheTotalSizeLimit;
        private bool cancelOperation /* = false */;

        public AsyncItemLoader(FolderMetaData folderInfo, TFSSourceControlProvider sourceControlProvider, long cacheTotalSizeLimit)
        {
            this.folderInfo = folderInfo;
            this.sourceControlProvider = sourceControlProvider;
            this.cacheTotalSizeLimit = cacheTotalSizeLimit;
        }

        public void Start()
        {
            ReadItemsInFolder(folderInfo);
        }

        public virtual void Cancel()
        {
            cancelOperation = true;
        }

        private void ReadItemsInFolder(FolderMetaData folder)
        {
            foreach (ItemMetaData item in folder.Items)
            {
                // Before reading further data, verify total pending size:

                // Wanted to move size check/data reading into a helper,
                // but then the cancel handling below
                // would have to be implemented in an awkward more indirect way...
                while (CalculateLoadedItemsSize(folderInfo) > CacheTotalSizeLimit)
                {
                    if (cancelOperation)
                        break;

                    // Do some waiting until hopefully parts of totalLoadedItemsSize
                    // got consumed (by consumer side, obviously).
                    Helper.CooperativeSleep(1000);
                }

                if (cancelOperation)
                    break;

                if (item.ItemType == ItemType.Folder)
                {
                    ReadItemsInFolder((FolderMetaData) item);
                }
                else if (!(item is DeleteMetaData))
                {
                    sourceControlProvider.ReadFileAsync(item);
                }
            }
        }

        /// <summary>
        /// Queries total data size of all items within the entire directory hierarchy
        /// (i.e. file content data that we gathered and that awaits retrieval by client side).
        /// </summary>
        /// <param name="folder">Base folder to calculate the hierarchical data items size of</param>
        /// <returns>Byte count currently occupied by data items below the base folder</returns>
        private long CalculateLoadedItemsSize(FolderMetaData folder)
        {
            long itemsSize = 0;

            foreach (ItemMetaData item in folder.Items)
            {
                if (item.ItemType == ItemType.Folder)
                {
                    itemsSize += CalculateLoadedItemsSize((FolderMetaData) item);
                }
                else if (item.DataLoaded)
                {
                    itemsSize += item.Base64DiffData.Length;
                }
            }
            return itemsSize;
        }

        private long CacheTotalSizeLimit
        {
            get
            {
                return cacheTotalSizeLimit;
            }
        }
    }
}
