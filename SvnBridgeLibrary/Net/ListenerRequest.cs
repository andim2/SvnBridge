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
                bool finished = (0 == headerLine.Length);
                if (finished)
                {
                    break;
                }
				ParseHeaderLine(headerLine);
			}

			ReadMessageBody(stream, buffer);

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
			byte[] bytes = new byte[Constants.BufferSize];

            int bytesRead = ReadToBufferIncremental(
                stream,
                buffer,
                bytes);

            bool isConnectionOK = IsConnectionOK(
                bytesRead);

            if (!(isConnectionOK))
            {
                Helper.DebugUsefulBreakpointLocation();
                return false;
            }

            return true;
		}

        private static int ReadToBufferIncremental(
            Stream stream,
            MemoryStream buffer,
            byte[] bytes)
        {
			int bytesRead = stream.Read(bytes, 0, bytes.Length);

            Helper.AppendToStream(buffer, bytes, bytesRead);

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
            int bytesReadTotal)
        {
            bool isConnectionOK = false;

            bool haveData = (0 != bytesReadTotal);
            if (haveData)
            {
                isConnectionOK = true;
            }

            return isConnectionOK;
        }

		private void ReadMessageBody(Stream stream,
									 MemoryStream buffer)
		{
			int contentLength = GetContentLength();

			for (; ; )
			{
                var contentLengthRead = buffer.Length - buffer.Position;
                var contentLengthMissing = contentLength - contentLengthRead;
				bool finished = (0 >= contentLengthMissing);

				if (finished)
				{
					break;
				}

				bool isReadOK = ReadToBuffer(stream, buffer);
				if (!(isReadOK))
				{
					break;
				}
			}

      // Optimized(?) handling details:
      // Create a buffer precisely sized to content length,
      // to be directly placed into a newly constructed
      // precisely sized *readonly* output stream,
      // while actively accessing
      // only a minor *part* of a potentially largish source stream.
			byte[] messageBody = new byte[contentLength];

			buffer.Read(messageBody, 0, messageBody.Length);

			inputStream = new MemoryStream(messageBody, false);
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
