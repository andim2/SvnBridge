using System;
using System.IO;

namespace Tests.Infrastructure
{
    public class ReadWriteMemoryStream : Stream
    {
        private MemoryStream inputStream = new MemoryStream();
        private MemoryStream outputStream = new MemoryStream();

        public override bool CanRead
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        public override bool CanSeek
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        public override bool CanWrite
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        public override long Length
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        public override long Position
        {
            get { throw new Exception("The method or operation is not implemented."); }
            set { throw new Exception("The method or operation is not implemented."); }
        }

        public void SetInput(byte[] input)
        {
            inputStream = new MemoryStream(input);
        }

        public byte[] GetOutput()
        {
            return outputStream.ToArray();
        }

        public override void Flush()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override int Read(byte[] buffer,
                                 int offset,
                                 int count)
        {
            return inputStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset,
                                  SeekOrigin origin)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override void SetLength(long value)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override void Write(byte[] buffer,
                                   int offset,
                                   int count)
        {
            outputStream.Write(buffer, offset, count);
        }
    }
}