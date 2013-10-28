namespace SvnBridge.SourceControl
{
    public class DeleteFolderMetaData : FolderMetaData
    {
        public override string ToString()
        {
            return "Delete: " + base.ToString();
        }
    }
}