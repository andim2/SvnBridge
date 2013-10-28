namespace SvnBridge
{
    using System.IO;

    public static class Constants
    {
        public const int BufferSize = 1024 * 32;
        public const int MaxPort = 65535;
        public const string ServerRootPath = "$/";
        public const string SvnVccPath = "/!svn/vcc/default";
        public const string FolderPropFile = ".svnbridge";
        public const string FolderPropFilePath = PropFolder + "/" + FolderPropFile;
        public const string LocalPrefix = @"C:\";
        public const string WorkspaceComment = "Temporary workspace for edit-merge-commit";
        public const string PropFolder = "..svnbridge";
    }
}