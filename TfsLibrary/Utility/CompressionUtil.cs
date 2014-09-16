using System.IO;
using System.IO.Compression;

namespace CodePlex.TfsLibrary.Utility
{
    public static class CompressionUtil
    {
        public static Pair<byte[], CompressionType> Compress(byte[] contents,
                                                             CompressionType compression)
        {
            if (compression == CompressionType.None)
                return new Pair<byte[], CompressionType>(contents, CompressionType.None);

            using (MemoryStream outputStream = new MemoryStream())
            {
                using (Stream compressionStream = MakeCompressionStream(outputStream, CompressionMode.Compress, compression))
                    compressionStream.Write(contents, 0, contents.Length);

                outputStream.Flush();
                outputStream.Position = 0;

                // Only use compressed value if it's smaller than the original
                if (outputStream.Length < contents.Length)
                {
                    contents = new byte[outputStream.Length];
                    outputStream.Read(contents, 0, contents.Length);
                }
                else
                    compression = CompressionType.None;
            }

            return new Pair<byte[], CompressionType>(contents, compression);
        }

        public static byte[] Decompress(byte[] contents,
                                        CompressionType compression)
        {
            if (compression == CompressionType.None)
                return contents;

            using (MemoryStream outputStream = new MemoryStream())
            {
                byte[] buffer = new byte[65536];
                int read;

                using (MemoryStream inputStream = new MemoryStream(contents, false))
                using (Stream compressionStream = MakeCompressionStream(inputStream, CompressionMode.Decompress, compression))
                    while ((read = compressionStream.Read(buffer, 0, buffer.Length)) > 0)
                        outputStream.Write(buffer, 0, read);

                outputStream.Flush();
                outputStream.Position = 0;

                buffer = new byte[outputStream.Length];
                outputStream.Read(buffer, 0, buffer.Length);
                return buffer;
            }
        }

        static Stream MakeCompressionStream(Stream innerStream,
                                            CompressionMode mode,
                                            CompressionType type)
        {
            if (type == CompressionType.Deflate)
                return new DeflateStream(innerStream, mode, true);
            if (type == CompressionType.GZip)
                return new GZipStream(innerStream, mode, true);
            return innerStream;
        }
    }
}