using System;
using System.Collections.Generic;

namespace CodePlex.TfsLibrary
{
    public class MergeEngine
    {
        int leftChunkIndex, leftChunkLineIndex;
        readonly List<DiffEngine.Chunk> leftChunks;
        int rightChunkIndex, rightChunkLineIndex;
        readonly List<DiffEngine.Chunk> rightChunks;

        MergeEngine(List<DiffEngine.Chunk> leftChunks,
                    List<DiffEngine.Chunk> rightChunks)
        {
            this.leftChunks = leftChunks;
            this.rightChunks = rightChunks;
        }

        bool IsFinished
        {
            get { return leftChunkIndex >= leftChunks.Count && rightChunkIndex >= rightChunks.Count; }
        }

        static MergeChangeType ChunkTypeToMergeChangeType(DiffEngine.ChunkType chunkType)
        {
            switch (chunkType)
            {
                case DiffEngine.ChunkType.Left:
                    return MergeChangeType.Delete;

                case DiffEngine.ChunkType.Right:
                    return MergeChangeType.Add;

                case DiffEngine.ChunkType.Same:
                    return MergeChangeType.Same;
            }

            throw new Exception("Unexpected chunk type " + chunkType);
        }

        static List<ChangeChunk> GetChangeChunks(IList<DiffEngine.Chunk> chunks)
        {
            List<ChangeChunk> results = new List<ChangeChunk>();

            for (int idx = 0; idx < chunks.Count - 1; idx++)
                if (chunks[idx].ChunkType == DiffEngine.ChunkType.Left && chunks[idx + 1].ChunkType == DiffEngine.ChunkType.Right)
                    results.Add(new ChangeChunk(chunks[idx], chunks[idx + 1]));

            return results;
        }

        void GetMergeText(MergeCallback callback)
        {
            while (!IsFinished)
            {
                string leftLine;
                MergeChangeType leftType;
                GetNextLineLeft(out leftLine, out leftType);

                string rightLine;
                MergeChangeType rightType;
                GetNextLineRight(out rightLine, out rightType);

                if (leftType == MergeChangeType.Add && rightType == MergeChangeType.Add)
                {
                    if (leftLine == rightLine)
                    {
                        IncrementIndexLeft();
                        IncrementIndexRight();
                        callback(leftLine);
                    }
                    else
                        throw new MergeConflictException();
                }
                else if (leftType == MergeChangeType.Add)
                {
                    IncrementIndexLeft();
                    callback(leftLine);
                }
                else if (rightType == MergeChangeType.Add)
                {
                    IncrementIndexRight();
                    callback(rightLine);
                }
                else if (leftType == MergeChangeType.Delete || rightType == MergeChangeType.Delete)
                {
                    if (leftLine != rightLine)
                        throw new Exception(
                            string.Format("Got {0}/{4} with different text: left = '{1}' ({2}/{3}), right = '{5}' ({6}/{7})",
                                          leftType, leftLine, leftChunkIndex, leftChunkLineIndex,
                                          rightType, rightLine, rightChunkIndex, rightChunkLineIndex));

                    IncrementIndexLeft();
                    IncrementIndexRight();
                }
                else if (leftType == MergeChangeType.Same && rightType == MergeChangeType.Same)
                {
                    if (leftLine != rightLine)
                        throw new Exception(
                            string.Format("Got Same/Same with different text: left = '{0}' ({1}/{2}), right = '{3}' ({4}/{5})",
                                          leftType, leftChunkIndex, leftChunkLineIndex,
                                          rightType, rightChunkIndex, rightChunkLineIndex));

                    IncrementIndexLeft();
                    IncrementIndexRight();
                    callback(leftLine);
                }
                else
                    throw new Exception(
                        string.Format("Unexpected state: left == {0} ({1}/{2}), right == {3} ({4}/{5})",
                                      leftType, leftChunkIndex, leftChunkLineIndex,
                                      rightType, rightChunkIndex, rightChunkLineIndex));
            }
        }

        static void GetNextLine(IList<DiffEngine.Chunk> chunks,
                                ref int index,
                                ref int lineIndex,
                                out string line,
                                out MergeChangeType changeType)
        {
            if (index >= chunks.Count)
            {
                line = null;
                changeType = MergeChangeType.NoContent;
                return;
            }

            line = chunks[index].Lines[lineIndex];
            changeType = ChunkTypeToMergeChangeType(chunks[index].ChunkType);
        }

        void GetNextLineLeft(out string leftLine,
                             out MergeChangeType leftType)
        {
            GetNextLine(leftChunks, ref leftChunkIndex, ref leftChunkLineIndex, out leftLine, out leftType);
        }

        void GetNextLineRight(out string rightLine,
                              out MergeChangeType rightType)
        {
            GetNextLine(rightChunks, ref rightChunkIndex, ref rightChunkLineIndex, out rightLine, out rightType);
        }

        static void IncrementIndex(IList<DiffEngine.Chunk> chunks,
                                   ref int index,
                                   ref int lineIndex)
        {
            ++lineIndex;

            if (lineIndex >= chunks[index].LineCount)
            {
                lineIndex = 0;
                ++index;
            }
        }

        void IncrementIndexLeft()
        {
            IncrementIndex(leftChunks, ref leftChunkIndex, ref leftChunkLineIndex);
        }

        void IncrementIndexRight()
        {
            IncrementIndex(rightChunks, ref rightChunkIndex, ref rightChunkLineIndex);
        }

        static List<DiffEngine.Chunk> InterleaveChunks(IList<DiffEngine.Chunk> chunks)
        {
            List<DiffEngine.Chunk> result = new List<DiffEngine.Chunk>(chunks.Count);

            for (int idx = 0; idx < chunks.Count; ++idx)
            {
                DiffEngine.Chunk oldChunk1 = chunks[idx];

                if (idx + 1 < chunks.Count &&
                    oldChunk1.ChunkType == DiffEngine.ChunkType.Left &&
                    chunks[idx + 1].ChunkType == DiffEngine.ChunkType.Right)
                {
                    DiffEngine.Chunk oldChunk2 = chunks[++idx];
                    int commonLineCount = Math.Min(oldChunk1.LineCount, oldChunk2.LineCount);

                    for (int line = 0; line < commonLineCount; ++line)
                    {
                        result.Add(oldChunk1.NewChunkFromSingleLine(line));
                        result.Add(oldChunk2.NewChunkFromSingleLine(line));
                    }

                    DiffEngine.Chunk biggerChunk = oldChunk1.LineCount > oldChunk2.LineCount ? oldChunk1 : oldChunk2;

                    if (biggerChunk.LineCount > commonLineCount)
                        result.Add(biggerChunk.NewChunkFromEnd(biggerChunk.LineCount - commonLineCount));
                }
                else
                    result.Add(oldChunk1);
            }

            return result;
        }

        public static string[] Merge(string[] original,
                                     string[] left,
                                     string[] right)
        {
            List<DiffEngine.Chunk> originalLeftChunks = DiffEngine.GetDiff(original, left);
            List<DiffEngine.Chunk> originalRightChunks = DiffEngine.GetDiff(original, right);

            VerifyNoConflicts(originalLeftChunks, originalRightChunks);

            List<DiffEngine.Chunk> leftChunks = InterleaveChunks(originalLeftChunks);
            List<DiffEngine.Chunk> rightChunks = InterleaveChunks(originalRightChunks);
            List<string> result = new List<string>();

            new MergeEngine(leftChunks, rightChunks).GetMergeText(
                delegate(string textLine)
                {
                    result.Add(textLine);
                });

            return result.ToArray();
        }

        static void VerifyNoConflicts(IList<DiffEngine.Chunk> leftChunks,
                                      IList<DiffEngine.Chunk> rightChunks)
        {
            List<ChangeChunk> leftChanges = GetChangeChunks(leftChunks);
            List<ChangeChunk> rightChanges = GetChangeChunks(rightChunks);

            foreach (ChangeChunk leftChange in leftChanges)
                foreach (ChangeChunk rightChange in rightChanges)
                {
                    // Is there any overlap?

                    if (rightChange.DeleteChunk.EndIndex < leftChange.DeleteChunk.StartIndex ||
                        rightChange.DeleteChunk.StartIndex > leftChange.DeleteChunk.EndIndex)
                        throw new MergeConflictException();

                    // Are they deleting different portions of the original?

                    if (rightChange.DeleteChunk.StartIndex != leftChange.DeleteChunk.StartIndex ||
                        rightChange.DeleteChunk.EndIndex != leftChange.DeleteChunk.EndIndex)
                        throw new MergeConflictException();

                    // Are they inserting the exact same content?

                    List<string> leftChunksLines = leftChange.AddChunk.Lines;
                    List<string> rightChunksLines = rightChange.AddChunk.Lines;

                    if (leftChunksLines.Count == rightChunksLines.Count)
                        for (int compareIndex = 0; compareIndex < leftChunksLines.Count; compareIndex++)
                            if (leftChunksLines[compareIndex] != rightChunksLines[compareIndex])
                                throw new MergeConflictException();
                }
        }

        class ChangeChunk
        {
            public readonly DiffEngine.Chunk AddChunk;
            public readonly DiffEngine.Chunk DeleteChunk;

            public ChangeChunk(DiffEngine.Chunk deleteChunk,
                               DiffEngine.Chunk addChunk)
            {
                DeleteChunk = deleteChunk;
                AddChunk = addChunk;
            }
        }

        // Inner types

        delegate void MergeCallback(string textLine);

        enum MergeChangeType
        {
            Same,
            Add,
            Delete,
            NoContent
        }
    }
}