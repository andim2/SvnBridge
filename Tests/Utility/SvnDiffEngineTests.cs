using Xunit;
using SvnBridge.Utility; // Helper.StreamCompare()
using System.IO;
using System;
using System.Collections.Generic;
using System.Security.Cryptography; // CryptoStream

namespace UnitTests
{
    public class SvnDiffEngineTests
    {
        private static SvnDiffWindow ConstructSvnDiffWindow(byte[] data, byte[] instructions)
        {
            SvnDiffWindow svnDiff = new SvnDiffWindow();

            svnDiff.DataSectionBytes = data;
            svnDiff.InstructionSectionBytes = instructions;

            return svnDiff;
        }

        [Fact]
        public void ApplySvnDiff_ThatWillExpand()
        {
            byte[] source = new byte[5002];
            source[5001] = 1;
            byte[] data = new byte[0];
            byte[] instructions = new byte[4] { 0x00 | 0, 0xA7, 0x0A, 0 }; // 10100111 00001010
            SvnDiffWindow svnDiff = ConstructSvnDiffWindow(data, instructions);

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(svnDiff, source, 0);

            Assert.Equal(5002, resultBytes.Length);
            Assert.Equal(1, resultBytes[5001]);
        }

        [Fact]
        public void ApplySvnDiff_WhereCopyFromTargetOverlapsWithDestination()
        {
            byte[] data = new byte[2] {1, 2};
            byte[] instructions = new byte[3] {0x80 | 2, 0x40 | 4, 0};
            SvnDiffWindow svnDiff = ConstructSvnDiffWindow(data, instructions);

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(svnDiff, new byte[0], 0);

            Assert.Equal(new byte[6] {1, 2, 1, 2, 1, 2}, resultBytes);
        }

        [Fact]
        public void ApplySvnDiff_WithCopyFromData()
        {
            byte[] source = new byte[0];
            byte[] data = new byte[2] {1, 2};
            byte[] instructions = new byte[1] {0x80 | 2};
            SvnDiffWindow svnDiff = ConstructSvnDiffWindow(data, instructions);

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(svnDiff, source, 0);

            Assert.Equal(new byte[2] {1, 2}, resultBytes);
        }

        [Fact]
        public void ApplySvnDiff_WithCopyFromSource()
        {
            byte[] source = new byte[4] {1, 2, 3, 4};
            byte[] data = new byte[0];
            byte[] instructions = new byte[2] {0x00 | 2, 2};
            SvnDiffWindow svnDiff = ConstructSvnDiffWindow(data, instructions);

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(svnDiff, source, 0);

            Assert.Equal(new byte[2] {3, 4}, resultBytes);
        }

        [Fact]
        public void ApplySvnDiff_WithCopyFromTarget()
        {
            byte[] data = new byte[1] {5};
            byte[] instructions = new byte[3] {0x80 | 1, 0x40 | 1, 0};
            SvnDiffWindow svnDiff = ConstructSvnDiffWindow(data, instructions);

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(svnDiff, new byte[0], 0);

            Assert.Equal(new byte[2] {5, 5}, resultBytes);
        }

        [Fact]
        public void ApplySvnDiff_WithSourceDataIndex()
        {
            byte[] source = new byte[4] {1, 2, 3, 4};
            byte[] data = new byte[0];
            byte[] opCodeAndLength = new byte[2] {2, 1};
            SvnDiffWindow svnDiff = ConstructSvnDiffWindow(data, opCodeAndLength);

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(svnDiff, source, 1);

            Assert.Equal(new byte[2] {3, 4}, resultBytes);
        }

        [Fact]
        public void CreateReplaceDiff()
        {
            byte[] source = new byte[4] { 1, 2, 3, 4 };

            SvnDiffWindow diff = SvnDiffEngine.CreateReplaceDiff(source, 0, source.Length);

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(diff, null, 1);
            Assert.Equal(source, resultBytes);
        }

        [Fact]
        public void CreateReplaceDiff_EmptyArray_ReturnsNull()
        {
            byte[] source = new byte[] {};

            SvnDiffWindow diff = SvnDiffEngine.CreateReplaceDiff(source, 0, source.Length);

            Assert.Null(diff);
        }

        [Fact]
        public void CreateReplaceDiff_SubsetOfData()
        {
            byte[] source = new byte[4] { 1, 2, 3, 4 };

            SvnDiffWindow diff = SvnDiffEngine.CreateReplaceDiff(source, 1, 2);

            byte[] resultBytes = SvnDiffEngine.ApplySvnDiff(diff, null, 1);
            Assert.Equal(new byte[] { 2, 3 }, resultBytes);
        }

        private static Stream GetSvnDiffDataStream(
            byte[] source)
        {
            var diffStream = SvnDiffParser.GetBase64SvnDiffDataStream(
                source);

            var cryptoStream = new CryptoStream(
                diffStream,
                new FromBase64Transform(),
                CryptoStreamMode.Read);

            var diffDataStream = cryptoStream;

            return diffDataStream;
        }

        [Fact]
        public void GetBase64SvnDiffData()
        {
            byte[] source = new byte[4] { 1, 2, 3, 4 };

            var diffStream = GetSvnDiffDataStream(
                source);
            byte[] result = SvnDiffParser.ApplySvnDiffsFromStream(diffStream, new byte[0]);
            Assert.Equal(source, result);
        }

        [Fact]
        public void GetBase64SvnDiffData_WithLargeData()
        {
            byte[] source = new byte[250000];
            for (int i = 0; i < source.Length; i++)
                source[i] = (byte)(i % 255);

            var diffStream = GetSvnDiffDataStream(
                source);
            byte[] result = SvnDiffParser.ApplySvnDiffsFromStream(diffStream, new byte[0]);
            Assert.Equal(source, result);
        }

        [Fact]
        public void GetBase64SvnDiffData_WithLargeData_DiffWindowsDoNotExceed100K()
        {
            byte[] source = new byte[150000];
            for (int i = 0; i < source.Length; i++)
                source[i] = (byte)(i % 255);

            var diffStream = GetSvnDiffDataStream(
                source);
            SvnDiffWindow[] diffs = SvnDiffEngine.ParseSvnDiff(diffStream);

            Assert.Equal(2, diffs.Length);
            // Depending on platform type (32/64bit), limit is either 100000
            // or 81920 (our standard LOH allocation amount, to combat fragmentation)
            int dataLength = (int)diffs[0].TargetViewLength;
            bool amount_ok = ((100000 == dataLength) || (81920 == dataLength));
            Assert.Equal(true, amount_ok);
        }

        [Fact]
        public void GetBase64SvnDiffData_WithEmptyData_ReturnsOnlySignature()
        {
            byte[] source = new byte[] { };

            var diffStream = GetSvnDiffDataStream(
                source);

            byte[] expected = new byte[] { (byte)'S', (byte)'V', (byte)'N', (byte)0 };
            MemoryStream expectedStream = new MemoryStream(expected, false);
            Assert.Equal(true, Helper.StreamCompare(
                diffStream,
                expectedStream));
        }

        [Fact]
        public void ApplySvnDiffsFromStream()
        {
            byte[] source = new byte[] { 1, 2, 3, 4 };
            List<SvnDiffWindow> diffs = new List<SvnDiffWindow>();
            diffs.Add(SvnDiffEngine.CreateReplaceDiff(source, 0, source.Length));
            var stream = GetSvnDiffDataStream(
                source);

            byte[] result = SvnDiffParser.ApplySvnDiffsFromStream(stream, new byte[0]);

            Assert.Equal(source, result);
        }

        [Fact]
        public void ApplySvnDiffsFromStream_WithMultipleDiffWindows()
        {
            byte[] source = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            List<SvnDiffWindow> diffs = new List<SvnDiffWindow>();
            diffs.Add(SvnDiffEngine.CreateReplaceDiff(source, 0, 4));
            diffs.Add(SvnDiffEngine.CreateReplaceDiff(source, 4, 4));
            var stream = GetSvnDiffDataStream(
                source);

            byte[] result = SvnDiffParser.ApplySvnDiffsFromStream(stream, new byte[0]);

            Assert.Equal(source, result);
        }

        [Fact]
        public void ApplySvnDiffsFromStream_WithNoDiffWindows()
        {
            byte[] source = new byte[] { };
            List<SvnDiffWindow> diffs = new List<SvnDiffWindow>();
            var stream = GetSvnDiffDataStream(
                source);

            byte[] result = SvnDiffParser.ApplySvnDiffsFromStream(stream, new byte[0]);

            Assert.Equal(source, result);
        }
    }
}
