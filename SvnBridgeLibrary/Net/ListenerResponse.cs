using System.Collections.Generic;
using System.IO;
using System.Text;
using SvnBridge.Interfaces;

namespace SvnBridge.Net
{
    public sealed class ListenerResponse : IHttpResponse
    {
        private readonly List<KeyValuePair<string, string>> headers;
        // XXX: FxCop complains that this class ought to implement IDisposable mechanisms
        // since member outputStream is IDisposable-based!

        // See VERY IMPORTANT comment at .OutputStream of interface!
        private readonly ListenerResponseStream outputStream;
        private Stream filter;
        private Encoding contentEncoding;
        private string contentType;
        private bool sendChunked;
        private int statusCode;

        public ListenerResponse(Stream stream)
        {
            headers = new List<KeyValuePair<string, string>>();
            outputStream = new ListenerResponseStream(this, stream);
            Filter = outputStream; // setup default HTTP entity-body "filter" Stream value
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
            // I assume we need to enforce use of *filter*
            // rather than *outputStream* here
            // (see also HTTP *Transfer*-Encoding vs. *Content*-Encoding!!).
            // IOW, filter chain:
            // Producer --> payload content compressor --> transfer mangler (chunked etc.) --> network stream.
            get { return filter; }
        }

        public Stream Filter
        {
            get { return filter; }
            set { filter = value; }
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
            if (!filter.Equals(outputStream))
            {
                filter.Flush();
                filter.Close();
            }
            outputStream.Flush();
            outputStream.Close();
        }

        #endregion
    }
}
