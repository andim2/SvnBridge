using System;
using System.Collections;
using System.Collections.Generic;

namespace CodePlex.TfsLibrary
{
    // C# implementation of McIlroy-Hunt Diff Algorithm

    public class DiffEngine
    {
        static int[] CreateEmptyMatchVector(int len)
        {
            int[] result = new int[len];

            for (int idx = 0; idx < len; idx++)
                result[idx] = -1;

            return result;
        }

        static int FindLowestEligibleMatch(IList<int> rightValues,
                                           int lastMatch)
        {
            if (lastMatch == -1)
                return rightValues[0];

            int lowestValue = int.MaxValue;

            // Future optimization: convert to binary search, for blank lines

            foreach (int bPosition in rightValues)
                if (bPosition > lastMatch)
                    lowestValue = bPosition;
                else
                    break;

            return (lowestValue == int.MaxValue ? -1 : lowestValue);
        }

        public static List<Chunk> GetDiff(string left,
                                          string right)
        {
            return GetDiff(GetStringLines(left), GetStringLines(right));
        }

        public static List<Chunk> GetDiff(string[] left,
                                          string[] right)
        {
            List<Chunk> results = new List<Chunk>();
            int[] leftMatchVector = GetLeftMatchVector(left, right);

            int leftIndex = 0;
            int rightIndex = 0;

            while (leftIndex < left.Length)
                if (leftMatchVector[leftIndex] == -1)
                    GetDiff_MissingLeftContent(ref leftIndex, leftMatchVector, left, results);
                else if (leftMatchVector[leftIndex] != rightIndex)
                    GetDiff_MissingRightContent(leftIndex, ref rightIndex, leftMatchVector, right, results);
                else
                    GetDiff_SameContent(ref leftIndex, ref rightIndex, leftMatchVector, left, results);

            // See if there is any left-over content on the right to be added

            if (rightIndex < right.Length)
                results.Add(new Chunk(ChunkType.Right, right, rightIndex, right.Length - 1));

            return results;
        }

        static void GetDiff_MissingLeftContent(ref int leftIndex,
                                               int[] leftMatchVector,
                                               string[] left,
                                               ICollection<Chunk> results)
        {
            int endLeft = leftIndex + 1;

            while (endLeft < left.Length && leftMatchVector[endLeft] == -1)
                endLeft++;

            results.Add(new Chunk(ChunkType.Left, left, leftIndex, endLeft - 1));
            leftIndex = endLeft;
        }

        static void GetDiff_MissingRightContent(int leftIndex,
                                                ref int rightIndex,
                                                int[] leftMatchVector,
                                                string[] right,
                                                ICollection<Chunk> results)
        {
            int endRight = leftMatchVector[leftIndex] - 1;
            results.Add(new Chunk(ChunkType.Right, right, rightIndex, endRight));
            rightIndex = endRight + 1;
        }

        static void GetDiff_SameContent(ref int leftIndex,
                                        ref int rightIndex,
                                        int[] leftMatchVector,
                                        string[] left,
                                        ICollection<Chunk> results)
        {
            int endLeft = leftIndex + 1;

            while (endLeft < left.Length && (leftMatchVector[endLeft - 1] + 1 == leftMatchVector[endLeft]))
                endLeft++;

            results.Add(new Chunk(ChunkType.Same, left, leftIndex, endLeft - 1));
            leftIndex = endLeft;
            rightIndex = leftMatchVector[endLeft - 1] + 1;
        }

        static int[] GetLeftMatchVector(string[] left,
                                        string[] right)
        {
            int leftStart = 0, rightStart = 0;
            int leftFinish = left.Length - 1;
            int rightFinish = right.Length - 1;
            int[] leftMatchVector = CreateEmptyMatchVector(left.Length);

            // Optimization: skip common elements at the front and back

            while (leftStart <= leftFinish &&
                   rightStart <= rightFinish &&
                   left[leftStart] == right[rightStart])
                leftMatchVector[leftStart++] = rightStart++;

            while (leftStart <= leftFinish &&
                   rightStart <= rightFinish &&
                   left[leftFinish] == right[rightFinish])
                leftMatchVector[leftFinish--] = rightFinish--;

            // Walk the left side and find right side matches

            Dictionary<string, List<int>> rightHashMap = GetRightHashMap(right, rightStart, rightFinish);
            int lastMatch = leftStart == 0 ? -1 : leftMatchVector[leftStart - 1];

            for (int idx = leftStart; idx <= leftFinish; ++idx)
            {
                string value = left[idx];

                if (rightHashMap.ContainsKey(value))
                {
                    int result = FindLowestEligibleMatch(rightHashMap[value], lastMatch);
                    leftMatchVector[idx] = result;

                    if (result != -1)
                        lastMatch = result;
                }
            }

            return leftMatchVector;
        }

        static Dictionary<string, List<int>> GetRightHashMap(string[] collection,
                                                             int start,
                                                             int finish)
        {
            Dictionary<string, List<int>> result = new Dictionary<string, List<int>>();

            for (int idx = finish; idx >= start; idx--)
            {
                string value = collection[idx];

                if (!result.ContainsKey(value))
                    result[value] = new List<int>();

                result[value].Add(idx);
            }

            return result;
        }

        static string[] GetStringLines(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        }

        public enum ChunkType
        {
            Same,
            Left,
            Right,
        }

        public class Chunk : IEnumerable<string>
        {
            readonly ChunkType chunkType;
            readonly string[] lines;
            readonly int startIndex;
            readonly int endIndex;

            public Chunk(ChunkType chunkType,
                         string[] lines,
                         int startIndex,
                         int endIndex)
            {
                this.chunkType = chunkType;
                this.lines = lines;
                this.startIndex = startIndex;
                this.endIndex = endIndex;
            }

            public ChunkType ChunkType
            {
                get { return chunkType; }
            }

            public int EndIndex
            {
                get { return endIndex; }
            }

            public int LineCount
            {
                get { return endIndex - startIndex + 1; }
            }

            public List<string> Lines
            {
                get
                {
                    List<string> result = new List<string>();

                    foreach (string line in this)
                        result.Add(line);

                    return result;
                }
            }

            public int StartIndex
            {
                get { return startIndex; }
            }

            public IEnumerator<string> GetEnumerator()
            {
                for (int idx = startIndex; idx <= endIndex; idx++)
                    yield return lines[idx];
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public Chunk NewChunkFromFront(int maxLines)
            {
                return new Chunk(chunkType, lines, startIndex, startIndex + Math.Min(maxLines, LineCount) - 1);
            }

            public Chunk NewChunkFromEnd(int maxLines)
            {
                return new Chunk(chunkType, lines, endIndex - Math.Min(maxLines, LineCount) + 1, endIndex);
            }

            public Chunk NewChunkFromSingleLine(int line)
            {
                int index = startIndex + line;
                return new Chunk(chunkType, lines, index, index);
            }
        }
    }
}