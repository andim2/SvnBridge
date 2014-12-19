namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public partial class Item
    {
        public Item(int itemId,
                    int changesetId,
                    string downloadToken,
                    ItemType itemType,
                    string serverPath)
        {
            itemid = itemId;
            cs = changesetId;
            durl = downloadToken;
            type = itemType;
            item = serverPath;
        }
    }
}