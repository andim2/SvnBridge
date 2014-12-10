using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;

namespace CodePlex.TfsLibrary
{
    public partial class AddElement
    {
        public AddElement(string path,
                          ItemTypeElement itemType,
                          byte[] value,
                          CompressionTypeElement compression)
        {
            pathField = path;
            typeField = itemType;
            valueField = value;
            compressionField = compression;
        }

        public static AddElement FromSourceItem(SourceItem item,
                                                string baseServerPath,
                                                string baseDirectory,
                                                IFileSystem fileSystem)
        {
            Pair<byte[], CompressionType> compressed = GetCompressedContents(item.LocalName, item.ItemType, fileSystem);

            return new AddElement(TfsUtil.LocalPathToServerPath(baseServerPath, baseDirectory, item.LocalName, item.ItemType),
                                  ToItemTypeElement(item.ItemType), compressed.Left, ToCompressionTypeElement(compressed.Right));
        }

        public static Pair<byte[], CompressionType> GetCompressedContents(string path,
                                                                          ItemType itemType,
                                                                          IFileSystem fileSystem)
        {
            if (itemType == ItemType.File)
            {
                byte[] contents = fileSystem.ReadAllBytes(path);
                return CompressionUtil.Compress(contents, CompressionType.Deflate);
            }

            return new Pair<byte[], CompressionType>(null, CompressionType.None);
        }

        public static CompressionType ToCompressionType(CompressionTypeElement type)
        {
            if (type == CompressionTypeElement.deflate)
                return CompressionType.Deflate;
            if (type == CompressionTypeElement.gzip)
                return CompressionType.GZip;
            return CompressionType.None;
        }

        public static CompressionTypeElement ToCompressionTypeElement(CompressionType type)
        {
            if (type == CompressionType.Deflate)
                return CompressionTypeElement.deflate;
            if (type == CompressionType.GZip)
                return CompressionTypeElement.gzip;
            return CompressionTypeElement.none;
        }

        public static ItemTypeElement ToItemTypeElement(ItemType type)
        {
            if (type == ItemType.File)
                return ItemTypeElement.file;
            return ItemTypeElement.folder;
        }
    }
}