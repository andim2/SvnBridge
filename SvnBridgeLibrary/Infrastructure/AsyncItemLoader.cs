using System.Threading;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.SourceControl;

namespace SvnBridge.Infrastructure
{
    public class AsyncItemLoader
    {
        private const long MAX_BUFFER_SIZE = 100000000;

        private readonly FolderMetaData folderInfo;
        private readonly TFSSourceControlProvider sourceControlProvider;
        private bool cancelOperation;

        public AsyncItemLoader(FolderMetaData folderInfo, TFSSourceControlProvider sourceControlProvider)
        {
            this.folderInfo = folderInfo;
            this.sourceControlProvider = sourceControlProvider;
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
                while (CalculateLoadedItemsSize(folderInfo) > MAX_BUFFER_SIZE)
                {
                    if (cancelOperation)
                        break;

                    Thread.Sleep(1000);
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
    }
}