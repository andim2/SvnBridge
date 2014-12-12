using Xunit;
using SvnBridge.Utility;
using System.IO;
using System;
using System.Collections.Generic;

namespace UnitTests
{
    public class SvnDiffEngineTests
    {
        [Fact]
        public void ApplySvnDiff_ThatWillExpand()
        {
            byte[] source = new byte[5002];
            source[5001] = 1;
            SvnDiff svnDiff = new SvnDiff();
            byte[] data = new byte[0];
            byte[] instructions = new byte[4] {0x00 | 0, 0xA7, 0x0A, 0}; // 10100111 00001010
            svnDiff.DataSectionBytes = data;
            svnDiff.InstructionSectionBytes = instructions;

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(svnDiff, source, 0);

            Assert.Equal(5002, resultBytes.Length);
            Assert.Equal(1, resultBytes[5001]);
        }

        [Fact]
        public void ApplySvnDiff_WhereCopyFromTargetOverlapsWithDestination()
        {
            SvnDiff svnDiff = new SvnDiff();
            byte[] data = new byte[2] {1, 2};
            byte[] instructions = new byte[3] {0x80 | 2, 0x40 | 4, 0};
            svnDiff.DataSectionBytes = data;
            svnDiff.InstructionSectionBytes = instructions;

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(svnDiff, new byte[0], 0);

            Assert.Equal(new byte[6] {1, 2, 1, 2, 1, 2}, resultBytes);
        }

        [Fact]
        public void ApplySvnDiff_WithCopyFromData()
        {
            byte[] source = new byte[0];
            SvnDiff svnDiff = new SvnDiff();
            byte[] data = new byte[2] {1, 2};
            byte[] instructions = new byte[1] {0x80 | 2};
            svnDiff.DataSectionBytes = data;
            svnDiff.InstructionSectionBytes = instructions;

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(svnDiff, source, 0);

            Assert.Equal(new byte[2] {1, 2}, resultBytes);
        }

        [Fact]
        public void ApplySvnDiff_WithCopyFromSource()
        {
            byte[] source = new byte[4] {1, 2, 3, 4};
            SvnDiff svnDiff = new SvnDiff();
            byte[] data = new byte[0];
            byte[] instructions = new byte[2] {0x00 | 2, 2};
            svnDiff.DataSectionBytes = data;
            svnDiff.InstructionSectionBytes = instructions;

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(svnDiff, source, 0);

            Assert.Equal(new byte[2] {3, 4}, resultBytes);
        }

        [Fact]
        public void ApplySvnDiff_WithCopyFromTarget()
        {
            SvnDiff svnDiff = new SvnDiff();
            byte[] data = new byte[1] {5};
            byte[] instructions = new byte[3] {0x80 | 1, 0x40 | 1, 0};
            svnDiff.DataSectionBytes = data;
            svnDiff.InstructionSectionBytes = instructions;

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(svnDiff, new byte[0], 0);

            Assert.Equal(new byte[2] {5, 5}, resultBytes);
        }

        [Fact]
        public void ApplySvnDiff_WithSouceDataIndex()
        {
            byte[] source = new byte[4] {1, 2, 3, 4};
            SvnDiff svnDiff = new SvnDiff();
            byte[] data = new byte[0];
            byte[] opCodeAndLength = new byte[2] {2, 1};
            svnDiff.DataSectionBytes = data;
            svnDiff.InstructionSectionBytes = opCodeAndLength;

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(svnDiff, source, 1);

            Assert.Equal(new byte[2] {3, 4}, resultBytes);
        }

        [Fact]
        public void CreateReplaceDiff()
        {
            byte[] source = new byte[4] { 1, 2, 3, 4 };

            SvnDiff diff = SvnDiffEngine.CreateReplaceDiff(source, 0, source.Length);

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(diff, null, 1);
            Assert.Equal(source, resultBytes);
        }

        [Fact]
        public void CreateReplaceDiff_EmptyArray_ReturnsNull()
        {
            byte[] source = new byte[] {};

            SvnDiff diff = SvnDiffEngine.CreateReplaceDiff(source, 0, source.Length);

            Assert.Null(diff);
        }

        [Fact]
        public void CreateReplaceDiff_SubsetOfData()
        {
            byte[] source = new byte[4] { 1, 2, 3, 4 };

            SvnDiff diff = SvnDiffEngine.CreateReplaceDiff(source, 1, 2);

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(diff, null, 1);
            Assert.Equal(new byte[] { 2, 3 }, resultBytes);
        }

        [Fact]
        public void GetBase64SvnDiffData()
        {
            byte[] source = new byte[4] { 1, 2, 3, 4 };

            string diff = SvnDiffParser.GetBase64SvnDiffData(source);

            MemoryStream diffStream = new MemoryStream(Convert.FromBase64String(diff));
            byte[] result = SvnDiffParser.ApplySvnDiffsFromStream(diffStream, new byte[0]);
            Assert.Equal(source, result);
        }

        [Fact]
        public void GetBase64SvnDiffData_WithLargeData()
        {
            byte[] source = new byte[250000];
            for (int i = 0; i < source.Length; i++)
                source[i] = (byte)(i % 255);

            string diff = SvnDiffParser.GetBase64SvnDiffData(source);
            
            MemoryStream diffStream = new MemoryStream(Convert.FromBase64String(diff));
            byte[] result = SvnDiffParser.ApplySvnDiffsFromStream(diffStream, new byte[0]);
            Assert.Equal(source, result);
        }

        [Fact]
        public void GetBase64SvnDiffData_WithLargeData_DiffWindowsDoNotExceed100K()
        {
            byte[] source = new byte[150000];
            for (int i = 0; i < source.Length; i++)
                source[i] = (byte)(i % 255);

            string diff = SvnDiffParser.GetBase64SvnDiffData(source);

            MemoryStream diffStream = new MemoryStream(Convert.FromBase64String(diff));
            SvnDiff[] diffs = SvnDiffEngine.ParseSvnDiff(diffStream);

            Assert.Equal(2, diffs.Length);
            Assert.Equal(100000, (int)diffs[0].TargetViewLength);
        }

        [Fact]
        public void GetBase64SvnDiffData_WithEmptyData_ReturnsOnlySignature()
        {
            byte[] source = new byte[] { };

            string diff = SvnDiffParser.GetBase64SvnDiffData(source);

            byte[] expected = new byte[] { (byte)'S', (byte)'V', (byte)'N', (byte)0 };
            Assert.Equal(Convert.ToBase64String(expected), diff);
        }

        [Fact]
        public void ApplySvnDiffsFromStream()
        {
            byte[] source = new byte[] { 1, 2, 3, 4 };
            List<SvnDiff> diffs = new List<SvnDiff>();
            diffs.Add(SvnDiffEngine.CreateReplaceDiff(source, 0, source.Length));
            MemoryStream stream = new MemoryStream(Convert.FromBase64String(SvnDiffParser.GetBase64SvnDiffData(source)));

            byte[] result = SvnDiffParser.ApplySvnDiffsFromStream(stream, new byte[0]);

            Assert.Equal(source, result);
        }

        [Fact]
        public void ApplySvnDiffsFromStream_WithMultipleDiffWindows()
        {
            byte[] source = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            List<SvnDiff> diffs = new List<SvnDiff>();
            diffs.Add(SvnDiffEngine.CreateReplaceDiff(source, 0, 4));
            diffs.Add(SvnDiffEngine.CreateReplaceDiff(source, 4, 4));
            MemoryStream stream = new MemoryStream(Convert.FromBase64String(SvnDiffParser.GetBase64SvnDiffData(source)));

            byte[] result = SvnDiffParser.ApplySvnDiffsFromStream(stream, new byte[0]);

            Assert.Equal(source, result);
        }

        [Fact]
        public void ApplySvnDiffsFromStream_WithNoDiffWindows()
        {
            byte[] source = new byte[] { };
            List<SvnDiff> diffs = new List<SvnDiff>();
            MemoryStream stream = new MemoryStream(Convert.FromBase64String(SvnDiffParser.GetBase64SvnDiffData(source)));

            byte[] result = SvnDiffParser.ApplySvnDiffsFromStream(stream, new byte[0]);

            Assert.Equal(source, result);
        }
    }
}
