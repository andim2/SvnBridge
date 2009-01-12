using Xunit;
using SvnBridge.Utility;

namespace UnitTests
{
    public class SvnDiffEngineTests
    {
        [Fact]
        public void TestApplySvnDiffThatWillExpand()
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
        public void TestApplySvnDiffWhereCopyFromTargetOverlapsWithDestination()
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
        public void TestApplySvnDiffWithCopyFromData()
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
        public void TestApplySvnDiffWithCopyFromSource()
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
        public void TestApplySvnDiffWithCopyFromTarget()
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
        public void TestApplySvnDiffWithSouceDataIndex()
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
    }
}