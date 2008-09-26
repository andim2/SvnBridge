using System.Collections.Generic;
using System.Threading;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;

namespace SvnBridge.Infrastructure
{
    public class AsyncItemLoader
    {
        private readonly FolderMetaData folderInfo;
        private readonly TFSSourceControlProvider sourceControlProvider;

        public AsyncItemLoader(FolderMetaData folderInfo, TFSSourceControlProvider sourceControlProvider)
        {
            this.folderInfo = folderInfo;
            this.sourceControlProvider = sourceControlProvider;
        }

        public void Start()
        {
            ReadItemsInFolder(folderInfo);
        }

        private void ReadItemsInFolder(FolderMetaData folder)
        {
            foreach (ItemMetaData item in folder.Items)
            {
                if (item.ItemType == ItemType.Folder)
                {
                    ReadItemsInFolder((FolderMetaData)item);
                }
                else if (!(item is DeleteMetaData))
                {
                    sourceControlProvider.ReadFileAsync(item);
                }
            }
        }
    }
}
