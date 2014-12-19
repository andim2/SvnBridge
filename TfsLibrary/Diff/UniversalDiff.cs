using System.Collections.Generic;

namespace CodePlex.TfsLibrary
{
    public static class UniversalDiff
    {
        public delegate void Callback(Block block);

        static bool BlockHasChanges(Block block)
        {
            return !(block.Chunks.Count == 1 && block.Chunks[0].ChunkType == DiffEngine.ChunkType.Same);
        }

        static Block CreateNewBlockFromSameChunk(DiffEngine.Chunk chunk,
                                                 ref int leftStart,
                                                 ref int rightStart)
        {
            DiffEngine.Chunk newChunk = chunk.NewChunkFromEnd(3);
            leftStart += chunk.LineCount - newChunk.LineCount;
            rightStart += chunk.LineCount - newChunk.LineCount;
            Block block = new Block(leftStart, rightStart);
            block.AddChunk(newChunk);
            return block;
        }

        public static void Diff(List<DiffEngine.Chunk> diff,
                                Callback callback)
        {
            Guard.ArgumentNotNull(diff, "diff");
            Guard.ArgumentNotNull(callback, "callback");

            if (diff.Count == 0)
                return;

            if (diff.Count == 1 && diff[0].ChunkType == DiffEngine.ChunkType.Same)
                return;

            int leftStart = 1;
            int rightStart = 1;
            Block block = null;

            for (int idx = 0; idx < diff.Count; idx++)
            {
                DiffEngine.Chunk chunk = diff[idx];

                if (chunk.ChunkType == DiffEngine.ChunkType.Same)
                {
                    if (block == null)
                    {
                        block = CreateNewBlockFromSameChunk(chunk, ref leftStart, ref rightStart);
                    }
                    else
                    {
                        if (idx == diff.Count - 1)
                        {
                            EndBlock(block, callback, chunk, ref leftStart, ref rightStart);
                            block = null;
                        }
                        else if (chunk.LineCount > 6)
                        {
                            EndBlock(block, callback, chunk, ref leftStart, ref rightStart);
                            block = CreateNewBlockFromSameChunk(chunk, ref leftStart, ref rightStart);
                        }
                        else
                            block.AddChunk(chunk);
                    }
                }
                else
                {
                    if (block == null)
                        block = new Block(leftStart, rightStart);

                    block.AddChunk(chunk);
                }
            }

            if (block != null && BlockHasChanges(block))
                callback(block);
        }

        static void EndBlock(Block block,
                             Callback callback,
                             DiffEngine.Chunk chunk,
                             ref int leftStart,
                             ref int rightStart)
        {
            leftStart += block.LeftContentLength;
            rightStart += block.RightContentLength;

            DiffEngine.Chunk newChunk = chunk.NewChunkFromFront(3);
            block.AddChunk(newChunk);
            callback(block);
        }

        public class Block
        {
            public List<DiffEngine.Chunk> Chunks;
            public int LeftContentLength;
            public int LeftStartLine;
            public int RightContentLength;
            public int RightStartLine;

            public Block(int leftStartLine,
                         int rightStartLine)
            {
                LeftStartLine = leftStartLine;
                LeftContentLength = 0;
                RightStartLine = rightStartLine;
                RightContentLength = 0;
                Chunks = new List<DiffEngine.Chunk>();
            }

            internal void AddChunk(DiffEngine.Chunk chunk)
            {
                Chunks.Add(chunk);

                switch (chunk.ChunkType)
                {
                    case DiffEngine.ChunkType.Left:
                        LeftContentLength += chunk.LineCount;
                        break;

                    case DiffEngine.ChunkType.Right:
                        RightContentLength += chunk.LineCount;
                        break;

                    case DiffEngine.ChunkType.Same:
                        LeftContentLength += chunk.LineCount;
                        RightContentLength += chunk.LineCount;
                        break;
                }
            }

            internal void AddChunkLastThreeLines(DiffEngine.Chunk chunk)
            {
                // TODO: This could be horribly inefficient... like, doubling the in-memory footprint
                // Consider adding a method to Chunk which can create a new chunk out of a piece of the
                // existing one.

                List<string> lines = chunk.Lines;

                if (lines.Count < 4)
                    AddChunk(chunk);
                else
                    AddChunk(new DiffEngine.Chunk(chunk.ChunkType, lines.ToArray(), lines.Count - 3, lines.Count - 1));
            }
        }
    }
}