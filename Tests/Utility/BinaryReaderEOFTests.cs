using System;
using System.IO;
using Xunit;

namespace SvnBridge.Utility
{
    public class BinaryReaderEOFTests
    {
        private byte[] CreateTestData(int size)
        {
            byte[] data = new byte[size];
            Random random = new Random();
            random.NextBytes(data);
            return data;
        }

        [Fact]
        public void TestEOFReturnsFalseIfNotEndOfStream()
        {
            byte[] testData = CreateTestData(10);
            BinaryReaderEOF reader = new BinaryReaderEOF(new MemoryStream(testData));

            bool result = reader.EOF;

            Assert.Equal(false, result);
        }

        [Fact]
        public void TestEOFReturnsTrueIfAtEndOfStream()
        {
            byte[] testData = CreateTestData(10);
            BinaryReaderEOF reader = new BinaryReaderEOF(new MemoryStream(testData));
            reader.ReadBytes(10);

            bool result = reader.EOF;

            Assert.Equal(true, result);
        }

        [Fact]
        public void TestEOFReturnsTrueWithZeroByteStream()
        {
            byte[] testData = CreateTestData(0);
            BinaryReaderEOF reader = new BinaryReaderEOF(new MemoryStream(testData));

            bool result = reader.EOF;

            Assert.Equal(true, result);
        }

        [Fact]
        public void TestReadByteReadsCorrectBytes()
        {
            byte[] testData = CreateTestData(10);
            BinaryReaderEOF reader = new BinaryReaderEOF(new MemoryStream(testData));

            for (int i = 0; i < testData.Length; i++)
            {
                Assert.Equal(testData[i], reader.ReadByte());
            }
        }

        [Fact]
        public void TestReadByteReadsCorrectBytesWhenReadPastBufferSize()
        {
            byte[] testData = CreateTestData(BinaryReaderEOF.BUF_SIZE + 10);
            BinaryReaderEOF reader = new BinaryReaderEOF(new MemoryStream(testData));

            for (int i = 0; i < testData.Length; i++)
            {
                Assert.Equal(testData[i], reader.ReadByte());
            }
        }

        [Fact]
        public void TestReadBytesReadsCorrectBytes()
        {
            byte[] testData = CreateTestData(10);
            BinaryReaderEOF reader = new BinaryReaderEOF(new MemoryStream(testData));

            byte[] result = reader.ReadBytes(testData.Length);

            for (int i = 0; i < testData.Length; i++)
            {
                Assert.Equal(testData[i], result[i]);
            }
        }

        [Fact]
        public void TestReadBytesReadsCorrectBytesIfReadingExactBufferSize()
        {
            byte[] testData = CreateTestData(BinaryReaderEOF.BUF_SIZE*2);
            BinaryReaderEOF reader = new BinaryReaderEOF(new MemoryStream(testData));

            byte[] result1 = reader.ReadBytes(BinaryReaderEOF.BUF_SIZE);
            byte[] result2 = reader.ReadBytes(BinaryReaderEOF.BUF_SIZE);

            for (int i = 0; i < result1.Length; i++)
            {
                Assert.Equal(testData[i], result1[i]);
            }

            for (int i = 0; i < result2.Length; i++)
            {
                Assert.Equal(testData[i + 1024], result2[i]);
            }
        }

        [Fact]
        public void TestReadBytesReadsCorrectBytesWhenReadsLargerThenBufferSize()
        {
            byte[] testData = CreateTestData(BinaryReaderEOF.BUF_SIZE*3);
            BinaryReaderEOF reader = new BinaryReaderEOF(new MemoryStream(testData));

            byte[] result = reader.ReadBytes(testData.Length);

            for (int i = 0; i < testData.Length; i++)
            {
                Assert.Equal(testData[i], result[i]);
            }
        }
    }
}