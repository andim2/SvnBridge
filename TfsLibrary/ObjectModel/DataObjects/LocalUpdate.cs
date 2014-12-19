namespace CodePlex.TfsLibrary.ObjectModel
{
    public class LocalUpdate
    {
        public int ItemId;
        public int LocalChangesetID;
        public string LocalName;

        public static LocalUpdate FromLocal(int itemId,
                                            string localName,
                                            int localChangesetID)
        {
            LocalUpdate result = new LocalUpdate();
            result.ItemId = itemId;
            result.LocalName = localName;
            result.LocalChangesetID = localChangesetID;
            return result;
        }

        public override string ToString()
        {
            return string.Format("id={0} csid={1} name={2}", ItemId, LocalChangesetID, LocalName);
        }
    }
}