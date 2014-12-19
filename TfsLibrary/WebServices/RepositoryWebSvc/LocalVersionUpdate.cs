namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public partial class LocalVersionUpdate
    {
        public LocalVersionUpdate(int itemId,
                                  string targetLocalItem,
                                  int localChangesetId)
        {
            itemid = itemId;
            tlocal = targetLocalItem;
            lver = localChangesetId;
        }
    }
}