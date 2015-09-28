using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using SvnBridge.Utility; // Helper.AppendToStream(), Utility.MemoryStreamLOHSanitized

namespace SvnBridge.Net
{
	public sealed class ListenerRequest : IHttpRequest
	{
		private readonly NameValueCollection headers;
		private string httpMethod;
    // XXX: FxCop complains that this class ought to implement IDisposable mechanisms
    // since member inputStream is IDisposable-based!
		private MemoryStream inputStream;
		private string path;
		private Uri url;

        public ListenerRequest(Stream stream, DefaultLogger logger)
		{
			headers = new NameValueCollection();

			ParseRequest(stream, logger);
		}


		public string ApplicationPath
		{
			get { return "/"; }
		}

		public NameValueCollection Headers
		{
			get { return headers; }
		}

		public string HttpMethod
		{
			get { return httpMethod; }
		}

		public Stream InputStream
		{
			get { return inputStream; }
		}

		public Uri Url
		{
			get
			{
				if (url == null)
				{
					BuildUrl();
				}

				return url;
			}
		}

		public string LocalPath
		{
			get
			{
				return Url.LocalPath;
			}
		}

		private void BuildUrl()
		{
			string host = Headers["host"];

			if (!String.IsNullOrEmpty(host) && !path.StartsWith("http"))
			{
				url = new Uri(String.Format("http://{0}{1}", host, path));
			}
			else
			{
				url = new Uri(path);
			}
		}

        /// <remarks>
        /// See also
        /// http://stackoverflow.com/questions/18564044/parsing-data-from-a-network-stream
        /// </remarks>
        private void ParseRequest(Stream stream, DefaultLogger logger)
		{
      // Improve fragmentation / efficiency issues
      // via one single globally shared stream
      // (during multiple parse activities)
      // for all subsequent I/O-decoupled parsing
      // of data from network stream here.
			MemoryStream buffer = new Utility.MemoryStreamLOHSanitized();

			bool isReadOK = ReadToBuffer(stream, buffer);
            if (!(isReadOK))
            {
                return;
            }

			string startLine = ReadLine(stream, buffer);
			ParseStartLine(startLine);

            for (; ; )
            {
                string headerLine = ReadLine(stream, buffer);
                bool needContinueParsing = (0 != headerLine.Length);
                if (!(needContinueParsing))
                {
                    break;
                }
				ParseHeaderLine(headerLine);
			}

			HandleMessageBody(stream, buffer);

            // Now that all content has been read (and actively parsed) into buffer,
            // we're finally able to have it trace logged if requested:
			if(Logging.TraceEnabled)
			{
                TraceRequest(logger, buffer);
			}
		}

        private void TraceRequest(DefaultLogger logger, Stream stream)
        {
            string message = SnitchStringFromStream(stream);
            var idxZero = message.IndexOf("\0");
            bool containsZero = (-1 != idxZero);
            if (containsZero)
            {
                message = message.Substring(0, idxZero);
            }
            logger.TraceMessage(message);
        }

        private static string SnitchStringFromStream(Stream stream)
        {
            string content;

            var positionBackup = stream.Position;
            try
            {
                stream.Position = 0;
                // NO "using" here (would do unwanted Close() of *external* stream)
                StreamReader reader = new StreamReader(stream);
                content = reader.ReadToEnd();
            }
            finally
            {
                stream.Position = positionBackup;
            }

            return content;
        }

		private static string ReadLine(Stream stream,
									   MemoryStream buffer)
		{
			int offset = (int)buffer.Position;

			int previousByte = -1;
			int nextByte = buffer.ReadByte();

			while (!(previousByte == '\r' && nextByte == '\n'))
			{
				int byteRead = buffer.ReadByte();

				if (byteRead == -1)
				{
					bool isReadOK = ReadToBuffer(stream, buffer);
                    if (!(isReadOK))
                    {
                        break;
                    }
				}
				else
				{
					previousByte = nextByte;
					nextByte = byteRead;
				}
			}

			return Encoding.ASCII.GetString(buffer.GetBuffer(), offset, (int)buffer.Position - offset - 2);
		}

        private static void ReadData(
            Stream stream,
            MemoryStream buffer,
            byte[] dataOut)
        {
            ReadData(
                stream,
                buffer,
                new ArraySegment<byte>(dataOut, 0, dataOut.Length));
        }

        private static void ReadData(
            Stream stream,
            MemoryStream buffer,
            ArraySegment<byte> dataSegOut)
        {
            var toBeRead = dataSegOut.Count;
            var avail = buffer.Length - buffer.Position;
            var missing = (toBeRead - avail);
            bool needNewData = (0 < missing);
            if (needNewData)
            {
                bool isReadOK = ReadToBuffer(
                    stream,
                    buffer,
                    missing);
                if (!(isReadOK))
                {
                    return;
                }
            }

            buffer.Read(dataSegOut.Array, dataSegOut.Offset, toBeRead);
        }

        /// <summary>
        /// Reads network stream data to a buffer.
        /// References:
        /// http://stackoverflow.com/questions/13097269/what-is-the-correct-way-to-read-from-networkstream-in-net
        /// </summary>
        /// <param name="stream">Stream to be read</param>
        /// <param name="buffer">MemoryStream to be written to</param>
        /// <returns>true in case any data could be read, else false (socket close, etc.).</returns>
		private static bool ReadToBuffer(Stream stream,
										 MemoryStream buffer)
		{
            return ReadToBuffer(
                stream,
                buffer,
                -1);
        }

        private static bool ReadToBuffer(
            Stream stream,
            MemoryStream buffer,
            long numToBeReadTotal)
        {
            long bytesReadTotal = 0;

			byte[] bytes = new byte[Constants.BufferSize];

            bool haveProperTransferLengthInfo = (-1 != numToBeReadTotal);
            var segmentSize = bytes.Length;
            for (; ; )
            {
                int bytesToBeReadThisTime = GetNumBytesToBeReadThisTime(
                    numToBeReadTotal,
                    bytesReadTotal,
                    segmentSize,
                    haveProperTransferLengthInfo);

                bool needMoreData = (0 < bytesToBeReadThisTime);
                if (!(needMoreData))
                {
                    break;
                }

                int bytesReadThisTime = ReadToBufferIncremental(
                    stream,
                    buffer,
                    bytes,
                    bytesToBeReadThisTime);

                bool wasSuccessfulRead = (0 < bytesReadThisTime);
                if (!(wasSuccessfulRead))
                {
                    break;
                }

                bytesReadTotal += bytesReadThisTime;
            }

            bool isConnectionOK = IsConnectionOK(
                bytesReadTotal);

            if (!(isConnectionOK))
            {
                Helper.DebugUsefulBreakpointLocation();
                return false;
            }

            return true;
		}

        /// <remarks>
        /// NetworkStream.Read() will block
        /// in case there's no data to be read
        /// (unless .ReceiveTimeout is hackishly/manually set to a low value).
        /// While we could manually check NetworkStream.DataAvailable property,
        /// for a generic-Stream interface this is not possible.
        /// Thus, for cases where we improperly do not know
        /// the length to be expected,
        /// we'll have to do a single read only
        /// (and thus potentially miss a part of receive data
        /// due to reading into a limited buffer only).
        /// </remarks>
        private static int GetNumBytesToBeReadThisTime(
            long numToBeReadTotal,
            long bytesReadTotal,
            int segmentSize,
            bool haveProperTransferLengthInfo)
        {
            int numBytesToBeReadThisTime;

            if (haveProperTransferLengthInfo)
            {
                int numBytesToBeReadRemaining = (int)(numToBeReadTotal - bytesReadTotal);
                numBytesToBeReadThisTime = Math.Min(segmentSize, numBytesToBeReadRemaining);
            }
            else
            {
                bool havePriorSuccessfulRead = (0 != bytesReadTotal);
                numBytesToBeReadThisTime = (havePriorSuccessfulRead) ? 0 : segmentSize;
            }

            return numBytesToBeReadThisTime;
        }

        private static int ReadToBufferIncremental(
            Stream stream,
            MemoryStream buffer,
            byte[] bytes,
            int bytesToBeRead)
        {
			int bytesRead = stream.Read(bytes, 0, bytesToBeRead);

            ArraySegment<byte> arrSeg = new ArraySegment<byte>(bytes, 0, bytesRead);
            Helper.AppendToStream(buffer, arrSeg);

            return bytesRead;
        }

        /// <summary>
        /// Almost comment-only helper.
        /// </summary>
        /// <remarks>
        /// http://www.codeproject.com/Answers/273194/TcpClient-NetworkStream-Read-Help-Stream-does-not
        /// "NetworkStream.Read will return 0 if and only if the connection has been terminated
        ///  (otherwise it will block until there is at least one byte)."
        /// </remarks>
        private static bool IsConnectionOK(
            long bytesReadTotal)
        {
            bool isConnectionOK = false;

            bool haveData = (0 != bytesReadTotal);
            if (haveData)
            {
                isConnectionOK = true;
            }

            return isConnectionOK;
        }

        private void HandleMessageBody(
            Stream stream,
            MemoryStream buffer)
        {
            bool chunked;
            int contentLength;
            GetMessageBodyContentSettings(
                out chunked,
                out contentLength);

            bool haveMessageBody = HaveMessageBodyRFC2616(
                chunked,
                contentLength);

            bool needReadMessageBody = NeedReadMessageBody(
                haveMessageBody);
            if (needReadMessageBody)
            {
                ReadMessageBody(
                    stream,
                    buffer,
                    chunked,
                    contentLength);
            }
        }

        /// <remarks>
        /// ATTENTION! at least our unit tests expect
        /// a valid InputStream object
        /// to get set up even in zero length body case!
        /// </remarks>
        private static bool NeedReadMessageBody(
            bool haveMessageBody)
        {
            bool needReadMessageBody;

            needReadMessageBody = (haveMessageBody);
            needReadMessageBody = true;

            return needReadMessageBody;
        }

        /// <remarks>
        /// RFC2616:
        /// "
        /// The presence of a message-body in a request is signaled by the inclusion
        /// of a Content-Length or Transfer-Encoding header field in the request's message-headers.
        /// "
        /// </remarks>
        private static bool HaveMessageBodyRFC2616(
            bool chunked,
            int contentLength)
        {
            bool seemToHaveTransferEncodingHeader = (chunked);
            bool haveTransferEncodingHeader = (seemToHaveTransferEncodingHeader);
            return ((haveTransferEncodingHeader) || (0 != contentLength));
        }

        private void GetMessageBodyContentSettings(
            out bool chunked,
            out int contentLength)
        {
            chunked = IsTransferEncodingChunked(Headers);
            contentLength = GetContentLength();
        }

        public static bool IsTransferEncodingChunked(
            NameValueCollection headers)
        {
            string headerTransferEncoding = headers["Transfer-Encoding"];
            bool chunked = ((null != headerTransferEncoding) && (headerTransferEncoding.Equals("chunked", StringComparison.OrdinalIgnoreCase)));
            return chunked;
        }

        private void ReadMessageBody(
            Stream stream,
            MemoryStream buffer,
            bool chunked,
            int contentLength)
		{
            if (chunked)
            {
                ReadMessageBody_chunked(stream, buffer);
            }
            else
            {
                ReadMessageBody_linear(stream, buffer, contentLength);
            }
		}

        private void ReadMessageBody_linear(
            Stream stream,
            MemoryStream buffer,
            int contentLength)
        {
            var posStart = buffer.Position;
			for (; ; )
			{
                var contentLengthRead = buffer.Length - posStart;
                var contentLengthMissing = contentLength - contentLengthRead;
				bool needNewData = (0 < contentLengthMissing);

				if (!(needNewData))
				{
					break;
				}

				bool isReadOK = ReadToBuffer(stream, buffer, contentLengthMissing);
				if (!(isReadOK))
				{
					break;
				}
			}

      ArraySegment<byte> arrSeg = new ArraySegment<byte>(buffer.GetBuffer(), (int)posStart, contentLength);
			AdoptAsReadOnlyInputStream(arrSeg);
		}

        /// <remarks>
        /// Optimized(?) handling details:
        /// pass stream's buffer into a newly created
        /// precisely sized *readonly* output stream.
        /// </remarks>
        private void AdoptAsReadOnlyInputStream(ArraySegment<byte> arrSeg)
        {
            inputStream = new MemoryStream(arrSeg.Array, arrSeg.Offset, arrSeg.Count, false);
        }

        /// <summary>
        /// Implements parsing of chunked HTTP payload.
        /// Since we likely do (intend to) signal HTTP/1.1 conformance
        /// (since several locations have hard-coded "HTTP/1.1" strings),
        /// we do need to support chunked transfers as well
        /// since that is a *required* feature of 1.1.
        /// </summary>
        /// <remarks>
        /// See also e.g. http://wiki.nginx.org/HttpChunkinModule
        /// Not really sure whether it's a good idea to go
        /// from cleanly chunked operation
        /// (incremental streamy handling via tiny memory chunks)
        /// to huge-blob-style operation.
        /// Anyway, at least currently
        /// I need to remain
        /// within the current implementation model
        /// of request body parsing...
        /// </remarks>
        private void ReadMessageBody_chunked(
            Stream stream,
            MemoryStream buffer)
        {
            var bodyPlain = new Utility.MemoryStreamLOHSanitized();
            byte[] chunk = new byte[Constants.AllocSize_AvoidLOHCatastrophy];
            byte[] eol = new byte[2];
            for (;;)
            {
                string lineChunkSize = ReadLine(stream, buffer);
                int chunkSize = ParseChunkSize(lineChunkSize);
                bool isMarkerChunkingEnded = (0 == chunkSize);
                bool haveDataChunk = (!(isMarkerChunkingEnded));
                if (haveDataChunk)
                {
                    bool canAccomodateChunk = (chunkSize <= chunk.Length);
                    if (!(canAccomodateChunk))
                    {
                        chunk = new byte[chunkSize];
                    }
                    ArraySegment<byte> arrSegChunk = new ArraySegment<byte>(chunk, 0, chunkSize);
                    ReadData(
                        stream,
                        buffer,
                        arrSegChunk);
                    Helper.AppendToStream(bodyPlain, arrSegChunk);
                }
                ReadData(
                    stream,
                    buffer,
                    eol);
                if (isMarkerChunkingEnded)
                {
                    break;
                }
            }
            ArraySegment<byte> arrSeg = new ArraySegment<byte>(bodyPlain.GetBuffer(), 0, (int)bodyPlain.Length);
            AdoptAsReadOnlyInputStream(arrSeg);
        }

        private static int ParseChunkSize(
            string lineChunkSize)
        {
            var chunkSize = int.Parse(lineChunkSize, System.Globalization.NumberStyles.HexNumber);
            return chunkSize;
        }

		private int GetContentLength()
		{
			int contentLength = 0;

			string contentLengthHeader = Headers["Content-Length"];
			if (!String.IsNullOrEmpty(contentLengthHeader))
			{
				int.TryParse(contentLengthHeader, out contentLength);
			}

			return contentLength;
		}

		private void ParseStartLine(string startLine)
		{
			string[] startLineParts = startLine.Split(' ');
			httpMethod = startLineParts[0].ToLowerInvariant();
			path = startLineParts[1];
			if (path.StartsWith("//"))
			{
				path = path.Substring(1);
			}
		}

		private void ParseHeaderLine(string headerLine)
		{
			int indexOf = headerLine.IndexOf(":");
			if (indexOf == -1)
				throw new ProtocolViolationException("Could not parse header line: " + headerLine);

			string headerName = headerLine.Substring(0, indexOf);
			string headerValue = null;
			if (headerLine.Length >= indexOf + 2)
				headerValue = headerLine.Substring(headerName.Length + 2);

			Headers.Add(headerName, headerValue);
		}
	}
}
