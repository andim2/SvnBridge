namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public partial class GetOperation
    {
        public GetOperation(int itemId,
                            string sourceLocalItem,
                            string targetLocalItem,
                            string serverItem,
                            ItemType itemType,
                            string downloadToken,
                            int localChangesetId,
                            int serverChangesetId)
        {
            itemid = itemId;
            slocal = sourceLocalItem;
            tlocal = targetLocalItem;
            titem = serverItem;
            type = itemType;
            durl = downloadToken;
            lver = localChangesetId;
            sver = serverChangesetId;
        }
    }
}