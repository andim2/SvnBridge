using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace CodePlex.TfsLibrary
{
    public partial class DeleteElement
    {
        public DeleteElement() {}

        public DeleteElement(string path,
                             int csid,
                             ItemTypeElement itemType)
        {
            pathField = path;
            csidField = csid;
            typeField = itemType;
        }

        public static DeleteElement FromSourceItem(SourceItem item,
                                                   string baseServerPath,
                                                   string baseDirectory)
        {
            return new DeleteElement(TfsUtil.LocalPathToServerPath(baseServerPath, baseDirectory, item.LocalName, item.ItemType),
                                     item.LocalChangesetId, item.ItemType == ItemType.File ? ItemTypeElement.file : ItemTypeElement.folder);
        }
    }
}