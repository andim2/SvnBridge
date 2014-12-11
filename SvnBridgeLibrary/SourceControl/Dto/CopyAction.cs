namespace SvnBridge.SourceControl.Dto
{
    public class CopyAction
    {
        public readonly string Path;
        public readonly string TargetPath;
        public bool Rename;

        public CopyAction(string path,
                          string targetPath,
                          bool rename)
        {
            Path = path;
            TargetPath = targetPath;
            Rename = rename;
        }
    }
}