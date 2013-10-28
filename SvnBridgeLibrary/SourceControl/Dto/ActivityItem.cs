using CodePlex.TfsLibrary.RepositoryWebSvc;

namespace SvnBridge.SourceControl.Dto
{
    public class ActivityItem
    {
        public readonly ActivityItemAction Action;
        public readonly ItemType FileType;
        public readonly string Path;
        public readonly string SourcePath;

        public ActivityItem(string path, ItemType fileType, ActivityItemAction action, string sourcePath)
        {
            Action = action;
            FileType = fileType;
            Path = path;
            SourcePath = sourcePath;
            if (SourcePath.StartsWith("$//"))
                SourcePath = Constants.ServerRootPath + SourcePath.Substring(3);
        }

        public ActivityItem(string path,
                            ItemType fileType,
                            ActivityItemAction action)
        {
            Path = path;
            FileType = fileType;
            Action = action;
        }
    }
}