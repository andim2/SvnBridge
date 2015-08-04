using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using SvnBridge.Utility; // Utility.MemoryStreamLOHSanitized

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

			ReadToBuffer(stream, buffer);

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
					ReadToBuffer(stream, buffer);
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
		private static void ReadToBuffer(Stream stream,
										 MemoryStream buffer)
		{
			int originalPosition = (int)buffer.Position;

			byte[] bytes = new byte[Constants.BufferSize];

			int bytesRead = stream.Read(bytes, 0, bytes.Length);

      var positionWriteStart = buffer.Length;
      var numToBeWritten = bytesRead;
      var requiredMinimumCapacity = positionWriteStart + numToBeWritten;

      bool needEnlargeCapacity = (buffer.Capacity < requiredMinimumCapacity);
			if (needEnlargeCapacity)
			{
#if false
            // AWFUL Capacity handling!! (GC catastrophy)
            // .Capacity value should most definitely *NEVER* be directly (manually) modified,
            // since framework ought to know best
            // how to increment .Capacity value in suitably future-proof-sized steps!
            // (read: it's NOT useful
            // to keep incrementing [read: keep actively reallocating!!]
            // a continuously aggregated perhaps 8MB .Capacity
            // by some perhaps 4273 Bytes each!)
            //
            // --> use .SetLength() API
            // since it internally calculates (whenever needed)
            // the next suitable .Capacity value.
				buffer.Capacity = requiredMinimumCapacity;
#else
          buffer.SetLength(requiredMinimumCapacity);
#endif
			}

			buffer.Position = positionWriteStart;

			buffer.Write(bytes, 0, numToBeWritten);

			buffer.Position = originalPosition;
		}

		private void ReadMessageBody(Stream stream,
									 MemoryStream buffer)
		{
			int contentLength = GetContentLength();

			for (; ; )
			{
				bool finished = ((buffer.Length - buffer.Position) >= contentLength);

				if (finished)
				{
					break;
				}

				ReadToBuffer(stream, buffer);
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
