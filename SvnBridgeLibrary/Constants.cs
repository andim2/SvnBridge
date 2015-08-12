namespace SvnBridge
{
    using System.IO;

    public static class Constants
    {
        // Try hard to do _all_ allocations within the entire process
        // as blocks sized _less than_ 85000 bytes
        // (internet research hints that it's _lt_, not _le_!),
        // since it would otherwise end up in non-compacting (fragmenting!)
        // LOH (Large Object Heap), resulting in easily achievable non-existent
        // .NET Quality of Service (in 32bit address space)
        // due to OutOfMem exception termination, causing required-atomic large
        // SVN operations (checkout) to _never_ succeed.
        // All allocations should prefer to use _exactly_ this size,
        // to enable simple reuse (--> no fragmentation splits)
        // of exactly these freed block sizes.
        // To add insult to injury, there's no programmatic way
        // to query the LOH threshold (see
        // http://stackoverflow.com/questions/4814452/determine-limit-for-large-object-heap-programmatically ),
        // thus we need to decide on our own magic value.
        // Not known yet whether it's preferable to use order-of-2-sized
        // values rather than a simple "84000",
        // but I assume that requesting
        // sufficiently closely page-sized chunks
        // i.e. compatible with order-of-2-sized values
        // (or possibly need to request something *slightly less*
        // than that due to heap management overhead bytes!?)
        // is indeed preferable.
        // Side note: manually calling GC.Collect()
        // is NOT recommended!! ( http://stackoverflow.com/a/13554984 )
        public const int AllocSize_AvoidLOHCatastrophy = (64+16)*1024; // 81920
        public const int BufferSize = AllocSize_AvoidLOHCatastrophy; // used to be: 1024 * 32; but changing to a more suitable size is ok...
        public const int MaxPort = 65535;
        public const string ServerRootPath = "$/";
        public const string SvnVccPath = "/!svn/vcc/default";
        public const string FolderPropFile = ".sbsvnpropsfile"; // see PropFolder comment
        public const string FolderPropFilePath = PropFolder + "/" + FolderPropFile;
        public const string LocalPrefix = @"C:\";
        public const string WorkspaceComment = "Temporary workspace for edit-merge-commit";

        // Avoid incompatibility-prone double-dot syntax, avoid using Subversion's own ".svn" prefix convention,
        // avoid special chars.
        // The current choice (".sbsvnprops") might still be a bit too inconvenient
        // since it is an exact substring of the .sbsvnpropsfile part above
        // (incomplete [buggy] string handling may yield false positives due to semi-matches),
        // but changing these names should not be done lightly
        // since these files are stored all over the place
        // within a repositories unchangeable history.
        // Thus, either have *really* good future-proof candidates or definitely avoid changing it.
        // See also "Problematic/incompatible "..svnbridge" dir name used for svn prop (properties) storage"
        //   http://svnbridge.codeplex.com/workitem/15330
        // ** WARNING ** WebDAV/SVN property storage
        // as currently implemented via TFS items
        // is rather unstable / buggy / semi-completed
        // and will remain so for some time -
        // Thereby it's recommended to avoid using properties
        // at least for valuable repositories, since any SCM action
        // will have persistent (and possibly bad) consequences
        // due to a repository being history-based.
        //public const string PropFolder = "..svnbridge";
        public const string PropFolder = ".sbsvnprops";

    }
}
