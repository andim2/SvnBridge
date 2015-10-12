using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography; // CryptoStream
using System.Text;

namespace SvnBridge.Utility
{
    public class SvnDiffParser
    {
        // Not sure what this is (are there any comments whatsoever in this project!??)
        // Perhaps it's a magic upper limit for diff chunks in SVN protocol?
        // Since it's not known,
        // make sure that any subsequent changes
        // still properly obey this undocumented pre-existing limit.
        // Hmm, but at least there's a unit test
        // GetBase64SvnDiffData_WithLargeData_DiffWindowsDoNotExceed100K()...
        private const int MAX_DIFF_SIZE = 100000;

        private static int diffChunkSizeMax = Math.Min(Constants.AllocSize_AvoidLOHCatastrophy, MAX_DIFF_SIZE);

        public static byte[] ApplySvnDiffsFromStream(Stream inputStream, byte[] sourceData)
        {
            SvnDiffWindow[] diffs = SvnDiffEngine.ParseSvnDiff(inputStream);
            byte[] fileData = new byte[0];
            // FIXME BUG!?: failing this check (i.e. if there were no diffs to be processed) will cause *empty* file data to be returned
            // (unless this is exactly the behaviour that is expected by the protocol when not encountering any svndiffs -
            // but then IMHO the "no diffs --> empty file" check should still be done *outside* of this diff-specific handler!).
            // However, Subversion says "After the header come one or more windows" (note "one"!).
            // Still, sounds like we ought to instantiate/return a fileData in case of diffs.Lengths, else return sourceData.
            if (diffs.Length > 0)
            {
                int sourceDataStartIndex = 0;
                foreach (SvnDiffWindow diff in diffs)
                {
                    byte[] newData = SvnDiffEngine.ApplySvnDiff(diff, sourceData, sourceDataStartIndex);
                    int newDataLen = newData.Length;
                    sourceDataStartIndex += newDataLen;
                    Array.Resize(ref fileData, fileData.Length + newDataLen);
                    Array.Copy(newData, 0, fileData, fileData.Length - newDataLen, newDataLen);
                }
            }
            return fileData;
        }

        /// <summary>
        /// Provides a base64 encoding stream
        /// for properly streamy, non-huge-blob operation
        /// (avoid OutOfMemoryException).
        /// http://stackoverflow.com/questions/19364644/base64-cryptostream-with-streamwriter-vs-convert-tobase64string
        /// http://stackoverflow.com/questions/2525533/is-there-a-base64stream-for-net-where
        /// </summary>
        public static Stream GetBase64SvnDiffDataStream(byte[] data)
        {
            Stream stream;

            MemoryStream dataStream = new MemoryStream(data, false);
            MemoryStream svnDiffStream = GetSvnDiffDataStream(dataStream);

            var cryptoStream = new CryptoStream(svnDiffStream, new ToBase64Transform(), CryptoStreamMode.Read);

            // I had pondered using BufferedStream here,
            // but since we are usually reading the entire content in one go
            // (rather than with delays in between),
            // buffering probably won't help a lot, right?

            stream = cryptoStream;

            return stream;
        }

        /// <remarks>
        /// Provides svn diff chunk data as a MemoryStream,
        /// wholly converted from an item data input stream.
        /// This helper is now at least *quite a bit* better
        /// than the previous implementation -
        /// however to enable properly fully incrementally streamy operation
        /// this conversion handling
        /// instead ought to be implemented
        /// as a *custom* Stream class doing on-the-fly conversion.
        /// </remarks>
        private static MemoryStream GetSvnDiffDataStream(Stream dataStream)
        {
            int diff_chunk_size_max = DiffChunkSizeMax;
            // Technically spoken the length of SVN signature below
            // is in _addition_ to the chunk size - it may exceed stream size.
            // But due to severe LOH issues our focus lies on equal alloc sizes
            // (_always_ doing equally-sized (Non-)LOH blocks)
            // thus we do prefer paying a potential stream resizing (doubling) penalty.

            // BinaryWriter using/IDispose specifics:
            // https://social.msdn.microsoft.com/Forums/en-US/81bb7197-60a1-4f2b-a6d8-1501a369b527/binarywriter-and-stream?forum=csharpgeneral
            MemoryStream svnDiffStream = new MemoryStream(diff_chunk_size_max);
            // Side note (warning): "using" of a BinaryWriter
            // will have caused
            // not only a Flush, but actually a Close() of underlying stream
            // once beyond disposal!
            /* using */ BinaryWriter svnDiffWriter = new BinaryWriter(svnDiffStream);
            {
                SvnDiffEngine.WriteSvnDiffSignature(svnDiffWriter);
                byte[] diff_chunk = new byte[diff_chunk_size_max];
                for (; ; )
                {
                    var lengthThisTime = dataStream.Read(diff_chunk, 0, diff_chunk.Length);
                    bool haveFurtherData = (0 < lengthThisTime);
                    if (!(haveFurtherData))
                    {
                        break;
                    }
                    SvnDiffWindow svnDiff = SvnDiffEngine.CreateReplaceDiff(diff_chunk, 0, lengthThisTime);
                    SvnDiffEngine.WriteSvnDiffWindow(svnDiff, svnDiffWriter);
                }
            }

#if false
            return new MemoryStream(svnDiffStream.GetBuffer(), 0, (int)svnDiffStream.Length, false);
#else
            svnDiffStream.Seek(0, SeekOrigin.Begin); // DON'T FORGET POSITION RESET!!
            return svnDiffStream;
#endif
        }

        private static int DiffChunkSizeMax
        {
            get
            {
                return diffChunkSizeMax;
            }
        }
    }
}
