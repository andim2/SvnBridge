using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SvnBridge.Utility
{
    public class SvnDiffParser
    {
        private const int MAX_DIFF_SIZE = 100000;

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
            using (MemoryStream svnDiffStream = new MemoryStream())
            {
                SvnDiffEngine.WriteSvnDiffSignature(svnDiffStream);
                while (index < data.Length)
                {
                    int length = data.Length - index;
                    if (length > MAX_DIFF_SIZE)
                        length = MAX_DIFF_SIZE;

                    SvnDiff svnDiff = SvnDiffEngine.CreateReplaceDiff(data, index, length);
                    SvnDiffEngine.WriteSvnDiff(svnDiff, svnDiffStream);

                    index += length;
                }
                byte[] diff = svnDiffStream.ToArray();
                return Convert.ToBase64String(diff);
            }
        }
    }
}