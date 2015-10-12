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
            SvnDiff[] diffs = SvnDiffEngine.ParseSvnDiff(inputStream);
            byte[] fileData = new byte[0];
            if (diffs.Length > 0)
            {
                int sourceDataStartIndex = 0;
                foreach (SvnDiff diff in diffs)
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
            int index = 0;
            int diff_chunk_size_max = DiffChunkSizeMax;
            // Technically spoken the length of SVN signature below
            // is in _addition_ to the chunk size - it may exceed stream size.
            // But due to severe LOH issues our focus lies on equal alloc sizes
            // (_always_ doing equally-sized (Non-)LOH blocks)
            // thus we do prefer paying a potential stream resizing (doubling) penalty.
            using (MemoryStream svnDiffStream = new MemoryStream(diff_chunk_size_max))
            {
                SvnDiffEngine.WriteSvnDiffSignature(svnDiffStream);
                while (index < data.Length)
                {
                    int length = data.Length - index;
                    if (length > diff_chunk_size_max)
                        length = diff_chunk_size_max;

                    SvnDiff svnDiff = SvnDiffEngine.CreateReplaceDiff(data, index, length);
                    SvnDiffEngine.WriteSvnDiff(svnDiff, svnDiffStream);

                    index += length;
                }

                byte[] diff = svnDiffStream.ToArray();
                return Convert.ToBase64String(diff);
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
