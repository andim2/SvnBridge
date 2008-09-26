namespace SvnBridge.SourceControl
{
    public class FindOrCreateResults
    {
        public ItemMetaData FirstItemAdded;
        public FolderMetaData FirstItemAddedFolder;
        public ItemMetaData Item;

        public void ClearItem()
        {
            if (Item is FolderMetaData)
                ((FolderMetaData)Item).Items.Clear();
        }

        public void RevertAddition()
        {
            if(FirstItemAdded!=null)
            {
                FirstItemAddedFolder.Items.Remove(FirstItemAdded);
            }
        }
    }
}