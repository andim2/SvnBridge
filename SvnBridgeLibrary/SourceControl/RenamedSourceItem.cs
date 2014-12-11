using CodePlex.TfsLibrary.ObjectModel;

namespace SvnBridge.SourceControl
{
    public class RenamedSourceItem : SourceItem
    {
        public string OriginalRemoteName;
        public int OriginalRevision;

        public RenamedSourceItem(SourceItem item,
                                 string originalRemoteName,
                                 int originalRevision)
        {
            ItemId = item.ItemId;
            ItemType = item.ItemType;
            RemoteName = item.RemoteName;
            RemoteDate = item.RemoteDate;
            DownloadUrl = item.DownloadUrl;
            RemoteChangesetId = item.RemoteChangesetId;
            LocalChangesetId = item.LocalChangesetId;
            OriginalRemoteName = originalRemoteName;
            OriginalRevision = originalRevision;
        }
    }
}