namespace SvnBridge.SourceControl
{
    public sealed class DeleteFolderMetaData : FolderMetaData
    {
        public override string ToString()
        {
            return "Delete: " + base.ToString();
        }
    }
}