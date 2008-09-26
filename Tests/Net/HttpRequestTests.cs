using System;
using System.IO;
using System.Text;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using Xunit;
using Tests;

namespace SvnBridge.Net
{
    public class HttpRequestTests
    {
        protected MyMocks stubs = new MyMocks();

        private class StubStream : Stream
        {
            private readonly MemoryStream stream;

            public StubStream(byte[] buffer)
            {
                stream = new MemoryStream(buffer);
            }

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

            public override void Flush()
            {
                throw new Exception("The method or operation is not implemented.");
            }

            public override int Read(byte[] buffer,
                                     int offset,
                                     int count)
            {
                if (stream.Position == stream.Length)
                {
                    throw new Exception("Attempted to read past stream.");
                }
                else
                {
                    return stream.Read(buffer, offset, count);
                }
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
                throw new Exception("The method or operation is not implemented.");
            }
        }

        [Fact]
        public void TestParseRequestWithContentLengthGreaterThanBufferSize()
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append("GET /foo/bar HTTP/1.1\r\n");
            buffer.Append("HOST: localhost:80801\r\n");
            buffer.AppendFormat("Content-Length: {0}\r\n", Constants.BufferSize + 1);
            buffer.Append("\r\n");
            buffer.Append("".PadRight(Constants.BufferSize + 1, '0'));
            StubStream stream = new StubStream(Encoding.ASCII.GetBytes(buffer.ToString()));

            Assert.DoesNotThrow(
                delegate { new ListenerRequest(stream, stubs.CreateObject<DefaultLogger>()); });
        }

        [Fact]
        public void TestRequestUrlExcludesDoubleLeadingSlash()
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append("GET //foo/bar HTTP/1.1\r\n");
            buffer.Append("HOST: localhost:8081\r\n");
            buffer.AppendFormat("Content-Length: {0}\r\n", 5);
            buffer.Append("\r\n");
            buffer.Append("12345");
            StubStream stream = new StubStream(Encoding.ASCII.GetBytes(buffer.ToString()));

            ListenerRequest request = new ListenerRequest(stream, stubs.CreateObject<DefaultLogger>());

            Assert.Equal<string>("/foo/bar", request.Url.LocalPath);
        }
    }
}