using System;
using System.IO;
using System.Text;
using System.Web;
using SvnBridge.Interfaces;

namespace SvnBridgeServer
{
    public class HttpResponseWrapper : IHttpResponse
    {
        private readonly HttpResponse response;

        public HttpResponseWrapper(HttpResponse response)
        {
            this.response = response;
        }

        #region IHttpResponse Members

        public Encoding ContentEncoding
        {
            get { return response.ContentEncoding; }
            set { response.ContentEncoding = value; }
        }

        public string ContentType
        {
            get { return response.ContentType; }
            set { response.ContentType = value; }
        }

        public Stream OutputStream
        {
            get { return response.OutputStream; }
        }

        public Stream Filter
        {
            get { return response.Filter; }
            set { response.Filter = value; }
        }

        public bool SendChunked
        {
            get { throw new NotSupportedException(); }
            set { response.BufferOutput = !value; }
        }

        public int StatusCode
        {
            get { return response.StatusCode; }
            set { response.StatusCode = value; }
        }

        public bool BufferOutput
        {
            get { return response.BufferOutput; }
            set { response.BufferOutput = value; }
        }

        public void AppendHeader(string name,
                                 string value)
        {
            response.AppendHeader(name, value);
        }

        public void ClearHeaders()
        {
            response.ClearHeaders();
        }

        public void Close()
        {
            response.Close();
        }

        #endregion
    }
}