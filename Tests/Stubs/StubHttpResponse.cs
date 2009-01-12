using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SvnBridge.Interfaces;

namespace UnitTests
{
    public class StubHttpResponse : IHttpResponse
    {
        private readonly List<KeyValuePair<string, string>> headers;
        private Encoding contentEncoding;
        private string contentType;
        private Stream outputStream;
        private bool sendChunked;
        private int statusCode;
        private bool bufferOutput;

        public StubHttpResponse()
        {
            headers = new List<KeyValuePair<string, string>>();
        }

        internal List<KeyValuePair<string, string>> Headers
        {
            get { return headers; }
        }

        internal string GetHeader(string header)
        {
            foreach (var hdr in headers)
                if (hdr.Key == header)
                    return hdr.Value;

            return null;
        }

        #region IHttpResponse Members

        public Encoding ContentEncoding
        {
            get { return contentEncoding; }
            set { contentEncoding = value; }
        }

        public string ContentType
        {
            get { return contentType; }
            set { contentType = value; }
        }

        public Stream OutputStream
        {
            get { return outputStream; }
            set { outputStream = value; }
        }

        public string Output
        {
            get { return Encoding.Default.GetString(((MemoryStream)OutputStream).ToArray()); }
        }

        public bool SendChunked
        {
            get { return sendChunked; }
            set { sendChunked = value; }
        }

        public int StatusCode
        {
            get { return statusCode; }
            set { statusCode = value; }
        }

        public void AppendHeader(string name, string value)
        {
            headers.Add(new KeyValuePair<string, string>(name, value));
        }

        public void ClearHeaders()
        {
            headers.Clear();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        #endregion

        public bool BufferOutput
        {
            get { return bufferOutput; }
            set { bufferOutput = value; }
        }

        public void Write(string content)
        {
            byte[] buffer = contentEncoding.GetBytes(content);

            outputStream.Write(buffer, 0, buffer.Length);
        }
    }
}