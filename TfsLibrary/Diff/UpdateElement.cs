using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.Utility;

namespace CodePlex.TfsLibrary
{
    public partial class UpdateElement
    {
        public UpdateElement(string path,
                             int csid,
                             byte[] value,
                             CompressionTypeElement compression)
        {
            pathField = path;
            csidField = csid;
            valueField = value;
            compressionField = compression;
        }

        public static UpdateElement FromSourceItem(SourceItem item,
                                                   string baseServerPath,
                                                   string baseDirectory,
                                                   IFileSystem fileSystem)
        {
            Pair<byte[], CompressionType> compressed = AddElement.GetCompressedContents(item.LocalName, item.ItemType, fileSystem);

            return new UpdateElement(TfsUtil.LocalPathToServerPath(baseServerPath, baseDirectory, item.LocalName, item.ItemType),
                                     item.LocalChangesetId, compressed.Left, AddElement.ToCompressionTypeElement(compressed.Right));
        }
    }
}