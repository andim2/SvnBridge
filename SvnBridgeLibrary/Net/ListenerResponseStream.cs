using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using SvnBridge.Utility;

namespace SvnBridge.Net
{
    public class ListenerResponseStream : Stream
    {
        protected bool flushed = false;
        protected bool headerWritten = false;
        protected int maxKeepAliveConnections;
        protected ListenerRequest request;
        protected ListenerResponse response;
        protected Stream stream;
        protected MemoryStream streamBuffer = new MemoryStream();

        public ListenerResponseStream(ListenerRequest request,
                                      ListenerResponse response,
                                      Stream stream,
                                      int maxKeepAliveConnections)
        {
            this.request = request;
            this.response = response;
            this.stream = stream;
            this.maxKeepAliveConnections = maxKeepAliveConnections;

            streamBuffer = new MemoryStream();
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override void Flush()
        {
            if (!flushed)
            {
                WriteHeaderIfNotAlreadyWritten();
                if (response.SendChunked)
                {
                    byte[] chunkFooter = Encoding.UTF8.GetBytes("0\r\n\r\n");
                    stream.Write(chunkFooter, 0, chunkFooter.Length);
                }
                else
                {
                    byte[] buffer = streamBuffer.ToArray();
                    stream.Write(buffer, 0, buffer.Length);
                }

                stream.Flush();
                flushed = true;
            }
        }

        public override int Read(byte[] buffer,
                                 int offset,
                                 int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset,
                                  SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer,
                                   int offset,
                                   int count)
        {
            if (response.SendChunked)
            {
                WriteHeaderIfNotAlreadyWritten();

                byte[] chunkHeader = Encoding.UTF8.GetBytes(string.Format("{0:x}", count) + "\r\n");
                byte[] chunkFooter = Encoding.UTF8.GetBytes("\r\n");

                stream.Write(chunkHeader, 0, chunkHeader.Length);
                stream.Write(buffer, offset, count);
                stream.Write(chunkFooter, 0, chunkFooter.Length);
            }
            else
            {
                streamBuffer.Write(buffer, offset, count);
            }
        }

        protected void WriteHeaderIfNotAlreadyWritten()
        {
            if (!headerWritten)
            {
                string statusCodeDescription;

                switch (response.StatusCode)
                {
                    case 204:
                        statusCodeDescription = "No Content";
                        break;
                    case 207:
                        statusCodeDescription = "Multi-Status";
                        break;
                    case 301:
                        statusCodeDescription = "Moved Permanently";
                        break;
                    case 401:
                        statusCodeDescription = "Authorization Required";
                        break;
                    case 404:
                        statusCodeDescription = "Not Found";
                        break;
                    case 405:
                        statusCodeDescription = "Method Not Allowed";
                        break;
                    case 500:
                        statusCodeDescription = "Internal Server Error";
                        break;
                    case 501:
                        statusCodeDescription = "Method Not Implemented";
                        break;
                    default:
                        statusCodeDescription = ((HttpStatusCode) response.StatusCode).ToString();
                        break;
                }

                StringBuilder buffer = new StringBuilder();
                StringWriter writer = new StringWriter(buffer);

                writer.WriteLine("HTTP/1.1 {0} {1}", response.StatusCode, statusCodeDescription);

                writer.WriteLine("Date: {0}", Helper.FormatDateB(DateTime.Now));
                writer.WriteLine("Server: Apache/2.0.59 (Win32) SVN/1.4.2 DAV/2");

                List<KeyValuePair<string, string>> headers = response.Headers;

                string xPadHeader = null;
                string connection = null;

                foreach (KeyValuePair<string, string> header in headers)
                {
                    if (header.Key == "X-Pad")
                    {
                        xPadHeader = header.Value;
                        continue;
                    }
                    else if (header.Key == "Connection")
                    {
                        connection = header.Value;
                        continue;
                    }
                    else
                    {
                        writer.WriteLine("{0}: {1}", header.Key, header.Value);
                    }
                }

                if (!response.SendChunked)
                {
                    writer.WriteLine("Content-Length: {0}", streamBuffer.Length);
                }
                else
                {
                    writer.WriteLine("Transfer-Encoding: chunked");
                }

                if (connection != null)
                {
                    writer.WriteLine("Connection: {0}", connection);
                }

                if (request.Headers["Connection"] != null)
                {
                    string[] connectionHeaderParts = request.Headers["Connection"].Split(',');
                    foreach (string directive in connectionHeaderParts)
                    {
                        if (directive.TrimStart() == "Keep-Alive")
                        {
                            writer.WriteLine("Keep-Alive: timeout=15, max={0}", maxKeepAliveConnections);
                            writer.WriteLine("Connection: Keep-Alive");
                        }
                    }
                }

                writer.WriteLine("Content-Type: {0}", response.ContentType);

                if (!String.IsNullOrEmpty(xPadHeader))
                {
                    writer.WriteLine("X-Pad: {0}", xPadHeader);
                }

                writer.WriteLine("");

                byte[] bufferBytes = Encoding.UTF8.GetBytes(buffer.ToString());

                stream.Write(bufferBytes, 0, bufferBytes.Length);

                headerWritten = true;
            }
        }
    }
}
