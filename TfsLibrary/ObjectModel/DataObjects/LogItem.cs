namespace CodePlex.TfsLibrary.ObjectModel
{
    public class LogItem
    {
        public SourceItemHistory[] History;
        public string LocalPath;
        public string ServerPath;

        public LogItem() {}

        public LogItem(string localPath,
                       string serverPath,
                       SourceItemHistory[] history)
        {
            LocalPath = localPath;
            ServerPath = serverPath;
            History = history;
        }
    }
}