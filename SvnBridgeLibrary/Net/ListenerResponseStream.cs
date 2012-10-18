using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using SvnBridge.Utility; // Helper

namespace SvnBridge.Net
{
    /// <summary>
    /// I don't know... somehow this class doesn't have proper separation of concerns
    /// (which led to all sorts of issues).
    /// Rather than implementing both chunked _and_ non-chunked operation,
    /// it should be specific class instantiations (either chunked or non-chunked) in advance,
    /// depending on response.SendChunked.
    /// And _then_ one could implement it in a way to form a new chunk per each Flush().
    /// Perhaps similar to what Acme.Serve.servlet.http.ChunkedOutputStream does.
    /// </summary>
    public class ListenerResponseStream : Stream
    {
        protected bool flushed = false;
        protected bool headerWritten = false;
        protected ListenerRequest request;
        protected ListenerResponse response;
        protected Stream stream;
        protected int maxKeepAliveConnections;
        protected MemoryStream streamBuffer;
        protected static readonly byte[] chunkFooterChunk = Encoding.UTF8.GetBytes("\r\n");
        protected static readonly byte[] chunkFooterFinalZeroChunk = Encoding.UTF8.GetBytes("0\r\n\r\n");

        public ListenerResponseStream(ListenerRequest request,
                                      ListenerResponse response,
                                      Stream stream,
                                      int maxKeepAliveConnections)
        {
            this.request = request;
            this.response = response;
            this.stream = stream;
            this.maxKeepAliveConnections = maxKeepAliveConnections;

            this.streamBuffer = CreateMemoryStream();
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

        /// <remarks>
        /// NOTE: this method could (and did!) get called _multiple_ times,
        /// thus it should better be made
        /// to not have single-invocation-only constraints.
        /// </remarks>
        public override void Flush()
        {
            if (!flushed)
            {
                WriteHeaderIfNotAlreadyWritten();
                if (!response.SendChunked)
                {
                    ForwardStreamBuffer();
                }

                flushed = true;
            }
            // Better invoke the member's Flush() unconditionally /
            // *outside* of our semi-dirty Flush() override handling above!
            stream.Flush();
        }

        public override void Close()
        {
            Flush(); // may write header!

            if (response.SendChunked)
            {
                flushed = false;
                stream.Write(chunkFooterFinalZeroChunk, 0, chunkFooterFinalZeroChunk.Length);
                Flush(); // ...and a second flush!
            }
            // FIXME: hmm... should we Close() our wrapped Stream member here, too!?
            base.Close();
        }

        private void ForwardStreamBuffer()
        {
            //byte[] buffer = streamBuffer.ToArray(); // *copy* of *used* internal container segment
            //stream.Write(buffer, 0, buffer.Length);
            //stream.Write(streamBuffer.GetBuffer() /* *non-copy* of full internal container length */, 0, (int)streamBuffer.Length);
            streamBuffer.WriteTo(stream);
            // Make sure to re-create (cleanly/fully discard all old content,
            // by discarding old object!):
            streamBuffer = CreateMemoryStream();
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
            // BUG: best to avoid maintaining dirty buggy *manual* state
            // within a supposedly-thin wrapper class!!
            // (nobody ever used to reset that member!!)
            // And it's not really reliable in general:
            // Any stream-modifying base method other than Write()
            // could cause a Flush() to be newly required
            // (i.e. our manual bool state would need to be reset!),
            // and we wouldn't detect it...
            flushed = false; // Write() modifies buffer --> RESET OUR DIRTY STATE HELPER!

            if (response.SendChunked)
            {
                WriteHeaderIfNotAlreadyWritten();

                byte[] chunkHeader = Encoding.UTF8.GetBytes(string.Format("{0:x}", count) + "\r\n");

                stream.Write(chunkHeader, 0, chunkHeader.Length);
                stream.Write(buffer, offset, count);
                stream.Write(chunkFooterChunk, 0, chunkFooterChunk.Length);
            }
            else
            {
                streamBuffer.Write(buffer, offset, count);
            }
        }

        private static MemoryStream CreateMemoryStream()
        {
            return new Utility.MemoryStreamLOHSanitized();
        }

        private static string GetStatusCodeDescription(int httpStatusCode)
        {
            string statusCodeDescription;

            switch (httpStatusCode)
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
                    statusCodeDescription = ((HttpStatusCode)httpStatusCode).ToString();
                    break;
            }

            return statusCodeDescription;
        }

        /// <remarks>
        /// See also
        /// http://stackoverflow.com/questions/2595460/how-can-i-set-transfer-encoding-to-chunked-explicitly-or-implicitly-in-an-asp#comment2744849_2711405
        /// </remarks>
        protected void WriteHeaderIfNotAlreadyWritten()
        {
            if (!headerWritten)
            {
                DoWriteHeader();

                headerWritten = true;
            }
        }

        private void DoWriteHeader()
        {
            string statusCodeDescription = GetStatusCodeDescription(response.StatusCode);

            // Use ctor variant for implicit (*internal*) StringBuilder:
            StringWriter writer = new StringWriter();

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

            // FIXME: we are potentially writing multiple Connection: headers! (see below)
            // Should be consolidating this by implementing a HTTP header helper
            // which writes specific HTTP headers (with the corresponding values joined via ", ")
            // iff the input values string array is non-empty.
            if (connection != null)
            {
                writer.WriteLine("Connection: {0}", connection);
            }

            string connectionHeader = request.Headers["Connection"];
            if (connectionHeader != null)
            {
                string[] connectionHeaderParts = connectionHeader.Split(',');
                foreach (string directive in connectionHeaderParts)
                {
                    if (directive.TrimStart() == "Keep-Alive")
                    {
                        // It seems that as long as our Listener.cs does an active socket close via TcpClient.Close(),
                        // we really should *not* pretend to fulfill persistent connections via a HTTP Keep-Alive reply.
                        // Subversion neon-debug-mask 511 indicates that Subversion is surprised about an interim socket close
                        // via its "Could not read status line" / "Persistent connection timed out, retrying" log
                        // despite requesting (and being falsely acknowledged!) HTTP Keep-Alive
                        // (side note: it became the default mechanism in HTTP/1.1).
                        // Hmm, despite advertising "close" Subversion 1.6.17 still attempts persistent connections.
                        // This as observed on SvnBridge/.NET 2.0.5xxx.
                        // Root cause probably is our Net Listener setup being based on IHttpRequest rather than
                        // a full HttpWebRequest, i.e. we're rolling our own implementation.
                        // Disabling the TcpClient.Close() does *not* help (Subversion hangs at subsequent request,
                        // probably since the socket is not being served properly on our side; TODO: investigate...).
                        // Search keywords: "ServicePoint", "HttpBehaviour", "DefaultConnectionLimit", "DefaultPersistentConnectionLimit", 
                        bool isHttpKeepAliveSupported = false;

                        if (isHttpKeepAliveSupported)
                        {
                            writer.WriteLine("Keep-Alive: timeout=15, max={0}", maxKeepAliveConnections);
                            writer.WriteLine("Connection: Keep-Alive");
                        }
                        else
                            // It *seems* HTTP header values are supposed to be treated case-insensitively,
                            // however "close" is spelt lower-case in most cases,
                            // thus write it in the more common variant:
                            writer.WriteLine("Connection: close");
                    }
                }
            }

            writer.WriteLine("Content-Type: {0}", response.ContentType);

            if (!String.IsNullOrEmpty(xPadHeader))
            {
                writer.WriteLine("X-Pad: {0}", xPadHeader);
            }

            writer.WriteLine("");

            string headersString = writer.ToString(); // debug convenience
            byte[] bufferBytes = Encoding.UTF8.GetBytes(headersString);

            stream.Write(bufferBytes, 0, bufferBytes.Length);
        }
    }
}
