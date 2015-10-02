using System;
using System.Collections.Generic;
using System.IO;
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
                    sourceDataStartIndex += newData.Length;
                    Array.Resize(ref fileData, fileData.Length + newData.Length);
                    Array.Copy(newData, 0, fileData, fileData.Length - newData.Length, newData.Length);
                }
            }
            return fileData;
        }

        public static string GetBase64SvnDiffData(byte[] data)
        {
            int diff_chunk_size_max = DiffChunkSizeMax;
            // Technically spoken the length of SVN signature below
            // is in _addition_ to the chunk size - it may exceed stream size.
            // But due to severe LOH issues our focus lies on equal alloc sizes
            // (_always_ doing equally-sized (Non-)LOH blocks)
            // thus we do prefer paying a potential stream resizing (doubling) penalty.

            // BinaryWriter using/IDispose specifics:
            // https://social.msdn.microsoft.com/Forums/en-US/81bb7197-60a1-4f2b-a6d8-1501a369b527/binarywriter-and-stream?forum=csharpgeneral
            using (MemoryStream svnDiffStream = new MemoryStream(diff_chunk_size_max))
            // Side note (warning): "using" of a BinaryWriter
            // will have caused
            // not only a Flush, but actually a Close() of underlying stream
            // once beyond disposal!
            using (BinaryWriter svnDiffWriter = new BinaryWriter(svnDiffStream))
            {
                SvnDiffEngine.WriteSvnDiffSignature(svnDiffWriter);
                int index = 0;
                for (; ; )
                {
                    int length = data.Length - index;
                    bool haveFurtherData = (0 < length);
                    if (!(haveFurtherData))
                    {
                        break;
                    }

                    if (length > diff_chunk_size_max)
                        length = diff_chunk_size_max;

                    SvnDiffWindow svnDiff = SvnDiffEngine.CreateReplaceDiff(data, index, length);
                    SvnDiffEngine.WriteSvnDiffWindow(svnDiff, svnDiffWriter);

                    index += length;
                }

                // Prefer passing direct GetBuffer()
                // to the array/offset/length variant of ToBase64String()
                // rather than passing a ToArray() _copy_ to ToBase64String(array).
                // See also
                // http://www.hightechtalks.com/dotnet-framework-winforms-controls/serializing-image-base64-string-best-222259.html
                return Convert.ToBase64String(svnDiffStream.GetBuffer(), 0, (int)svnDiffStream.Length);
            }
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
