using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace SvnBridge.SourceControl
{
    public class MergeActivityResponseItem
    {
        public string Path;
        public ItemType Type;

        public MergeActivityResponseItem(ItemType type,
                                         string path)
        {
            Type = type;
            Path = path;
        }
    }
}