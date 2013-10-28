using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;

namespace SvnBridge.Net
{
	public class ListenerRequest : IHttpRequest
	{
		private readonly NameValueCollection headers;
		private string httpMethod;
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

        private void ParseRequest(Stream stream, DefaultLogger logger)
		{
			MemoryStream buffer = new MemoryStream();

			ReadToBuffer(stream, buffer);

			string startLine = ReadLine(stream, buffer);
			ParseStartLine(startLine);

			string headerLine = ReadLine(stream, buffer);
			while (headerLine != String.Empty)
			{
				ParseHeaderLine(headerLine);
				headerLine = ReadLine(stream, buffer);
			}

			ReadMessageBody(stream, buffer);
			if(Logging.TraceEnabled)
			{
				long position = buffer.Position;
				buffer.Position = 0;
				byte[]bytes= new byte[position];
				StreamReader reader = new StreamReader(buffer);
				string message = reader.ReadToEnd();
				if (message.Contains("\0"))
				{
					message = message.Substring(0, message.IndexOf("\0"));
				}
				logger.TraceMessage(message);
				buffer.Position = position;
			}
		}

		private static string ReadLine(Stream stream,
									   MemoryStream buffer)
		{
			int offset = (int)buffer.Position;

			int previousByte = -1;
			int nextByte = buffer.ReadByte();

			while (!(previousByte == 13 && nextByte == 10))
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

		private static void ReadToBuffer(Stream stream,
										 MemoryStream buffer)
		{
			int originalPosition = (int)buffer.Position;

			byte[] bytes = new byte[Constants.BufferSize];

			int bytesRead = stream.Read(bytes, 0, bytes.Length);

			int availableCapacity = buffer.Capacity - (int)buffer.Length;

			if (availableCapacity < bytesRead)
			{
				buffer.Capacity += (bytesRead - availableCapacity);
			}

			buffer.Position = buffer.Length;

			buffer.Write(bytes, 0, bytesRead);

			buffer.Position = originalPosition;
		}

		private void ReadMessageBody(Stream stream,
									 MemoryStream buffer)
		{
			int contentLength = GetContentLength();

			bool finished = ((buffer.Length - buffer.Position) >= contentLength);

			while (!finished)
			{
				ReadToBuffer(stream, buffer);

				finished = ((buffer.Length - buffer.Position) >= contentLength);
			}

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