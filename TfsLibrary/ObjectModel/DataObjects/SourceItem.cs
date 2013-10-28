using System;
using System.IO;
using System.Runtime.CompilerServices;
using CodePlex.TfsLibrary.RepositoryWebSvc;

[assembly : InternalsVisibleTo("UnitTest.CodePlexClientLibrary")]

namespace CodePlex.TfsLibrary.ObjectModel
{
    [Serializable]
    public class SourceItem : IComparable<SourceItem>
    {
        public string DownloadUrl;
        public int ItemId;
        public ItemType ItemType;
        public int LocalChangesetId;
        public int LocalConflictChangesetId;
        public string LocalConflictTextBaseName;
        public SourceItemStatus LocalItemStatus;
        public string LocalName;
        public string LocalTextBaseName;
        public SourceItemStatus OriginalLocalItemStatus;
        public int RemoteChangesetId;
        public DateTime RemoteDate;
        public SourceItemStatus RemoteItemStatus;
        public string RemoteName;
        public long RemoteSize;

        int IComparable<SourceItem>.CompareTo(SourceItem other)
        {
            string myName = LocalName ?? RemoteName;
            string otherName = other.LocalName ?? other.RemoteName;
            int nameCompare = string.Compare(myName, otherName, true);

            if (nameCompare != 0)
                return nameCompare;

            return ((int)RemoteItemStatus - (int)other.RemoteItemStatus);
        }

        public static SourceItem FromItem(int itemId,
                                          ItemType itemType,
                                          SourceItemStatus localStatus,
                                          SourceItemStatus originalStatus,
                                          string localName,
                                          string localTextBaseName,
                                          int localChangesetId,
                                          int localConflictChangesetId,
                                          string localConflictTextBaseName,
                                          SourceItemStatus remoteStatus,
                                          string remoteName,
                                          int remoteChangesetId,
                                          string downloadUrl)
        {
            SourceItem result = new SourceItem();

            result.ItemId = itemId;
            result.ItemType = itemType;
            result.LocalItemStatus = localStatus;
            result.OriginalLocalItemStatus = originalStatus;
            result.LocalName = localName;
            result.LocalTextBaseName = localTextBaseName;
            result.LocalChangesetId = localChangesetId;
            result.LocalConflictChangesetId = localConflictChangesetId;
            result.LocalConflictTextBaseName = localConflictTextBaseName;
            result.RemoteItemStatus = remoteStatus;
            result.RemoteName = remoteName;
            result.RemoteChangesetId = remoteChangesetId;
            result.RemoteSize = 0;
            result.RemoteDate = DateTime.Now;
            result.DownloadUrl = downloadUrl;

            return result;
        }

        internal static SourceItem FromLocalDirectory(int itemId,
                                                      SourceItemStatus localStatus,
                                                      SourceItemStatus originalStatus,
                                                      string localName,
                                                      int localChangesetId)
        {
            return FromLocalItem(itemId, ItemType.Folder, localStatus, originalStatus, localName, null,
                                 localChangesetId, Constants.NullChangesetId, null);
        }

        internal static SourceItem FromLocalFile(int itemId,
                                                 SourceItemStatus localStatus,
                                                 SourceItemStatus originalStatus,
                                                 string localName,
                                                 string localTextBaseName,
                                                 int localChangesetId,
                                                 int localConflictChangesetId,
                                                 string localConflictTextBaseName)
        {
            return FromLocalItem(itemId, ItemType.File, localStatus, originalStatus, localName, localTextBaseName,
                                 localChangesetId, localConflictChangesetId, localConflictTextBaseName);
        }

        public static SourceItem FromLocalItem(int itemId,
                                               ItemType itemType,
                                               SourceItemStatus localStatus,
                                               SourceItemStatus originalStatus,
                                               string localName,
                                               string localTextBaseName,
                                               int localChangesetId,
                                               int localConflictChangesetId,
                                               string localConflictTextBaseName)
        {
            SourceItem result = new SourceItem();

            result.ItemId = itemId;
            result.ItemType = itemType;
            result.LocalItemStatus = localStatus;
            result.OriginalLocalItemStatus = originalStatus;
            result.LocalName = localName;
            result.LocalTextBaseName = localTextBaseName;
            result.LocalChangesetId = localChangesetId;
            result.LocalConflictChangesetId = localConflictChangesetId;
            result.LocalConflictTextBaseName = localConflictTextBaseName;

            return result;
        }

        internal static SourceItem FromLocalPath(string localName)
        {
            if (File.Exists(localName))
                return FromLocalFile(Constants.NullItemId, SourceItemStatus.Unversioned, SourceItemStatus.Unversioned, localName,
                                     null, Constants.NullChangesetId, Constants.NullChangesetId, null);
            else
                return FromLocalDirectory(Constants.NullItemId, SourceItemStatus.Unversioned, SourceItemStatus.Unversioned,
                                          localName, Constants.NullChangesetId);
        }

        public static SourceItem FromRemoteItem(Item item)
        {
            return FromRemoteItem(item.itemid, item.type, item.item, item.cs, item.len, item.date, null);
        }

        public static SourceItem FromRemoteItem(int itemId,
                                                ItemType itemType,
                                                string remoteName,
                                                int remoteChangesetId,
                                                long remoteSize,
                                                DateTime remoteDate,
                                                string downloadUrl)
        {
            return FromRemoteItem(itemId, itemType, SourceItemStatus.Unmodified, remoteName, remoteChangesetId, remoteSize, remoteDate, downloadUrl);
        }

        public static SourceItem FromRemoteItem(int itemId,
                                                ItemType itemType,
                                                SourceItemStatus remoteStatus,
                                                string remoteName,
                                                int remoteChangesetId,
                                                long remoteSize,
                                                DateTime remoteDate,
                                                string downloadUrl)
        {
            SourceItem result = new SourceItem();

            result.ItemId = itemId;
            result.ItemType = itemType;
            result.RemoteItemStatus = remoteStatus;
            result.RemoteName = remoteName;
            result.RemoteChangesetId = remoteChangesetId;
            result.RemoteSize = remoteSize;
            result.RemoteDate = remoteDate;
            result.DownloadUrl = downloadUrl;

            return result;
        }

        public override string ToString()
        {
            string result = string.Format("id={0} type={1}", ItemId, ItemType);

            if (LocalName != null)
                result += string.Format(" l_name={0}", LocalName);

            if (LocalItemStatus != SourceItemStatus.None)
                result += string.Format(" l_cs={0} l_st={1}", LocalChangesetId, LocalItemStatus);

            if (RemoteName != null)
                result += string.Format(" r_name={0}", RemoteName);

            if (RemoteItemStatus != SourceItemStatus.None)
                result += string.Format(" r_cs={0} r_st={1}", RemoteChangesetId, RemoteItemStatus);

            return result;
        }
    }
}