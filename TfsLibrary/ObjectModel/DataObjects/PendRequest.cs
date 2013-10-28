using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary.ObjectModel
{
    public class PendRequest
    {
        public int CodePage;
        public ItemType ItemType;
        public string LocalName;
        public PendRequestType RequestType;
        public string TargetName;

        PendRequest() {}

        public static PendRequest AddFile(string localName,
                                          int codePage)
        {
            PendRequest result = new PendRequest();
            result.LocalName = localName;
            result.RequestType = PendRequestType.Add;
            result.ItemType = ItemType.File;
            result.CodePage = codePage;
            return result;
        }

        public static PendRequest AddFolder(string localName)
        {
            PendRequest result = new PendRequest();
            result.LocalName = localName;
            result.RequestType = PendRequestType.Add;
            result.ItemType = ItemType.Folder;
            result.CodePage = TfsUtil.CodePage_ANSI;
            return result;
        }

        public static PendRequest Copy(string localName,
                                       string targetName)
        {
            PendRequest result = new PendRequest();
            result.LocalName = localName;
            result.TargetName = targetName;
            result.RequestType = PendRequestType.Copy;
            return result;
        }

        public static PendRequest Delete(string localName)
        {
            PendRequest result = new PendRequest();
            result.LocalName = localName;
            result.RequestType = PendRequestType.Delete;
            return result;
        }

        public static PendRequest Edit(string localName)
        {
            PendRequest result = new PendRequest();
            result.LocalName = localName;
            result.RequestType = PendRequestType.Edit;
            return result;
        }

        public static PendRequest Rename(string localName,
                                         string targetName)
        {
            PendRequest result = new PendRequest();
            result.LocalName = localName;
            result.TargetName = targetName;
            result.RequestType = PendRequestType.Rename;
            return result;
        }
    }
}