using System.Collections.Generic;
using System.IO;
using System.Text;
using SvnBridge.Interfaces;

namespace SvnBridge.Net
{
    public class ListenerResponse : IHttpResponse
    {
        private readonly List<KeyValuePair<string, string>> headers;
        private readonly ListenerResponseStream outputStream;
        private Encoding contentEncoding;
        private string contentType;
        private bool sendChunked;
        private int statusCode;

        public ListenerResponse(ListenerRequest request,
                                Stream stream)
        {
            headers = new List<KeyValuePair<string, string>>();
            outputStream = new ListenerResponseStream(request, this, stream, 100);
        }

        internal List<KeyValuePair<string, string>> Headers
        {
            get { return headers; }
        }

        #region IHttpResponse Members

        public void AppendHeader(string name,
                                 string value)
        {
            headers.Add(new KeyValuePair<string, string>(name, value));
        }

        public void ClearHeaders()
        {
            headers.Clear();
        }

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

        public bool BufferOutput
        {
            get { return sendChunked == false; }
            set { }
        }

        public void Close()
        {
            outputStream.Flush();
            outputStream.Close();
        }

        #endregion
    }
}
