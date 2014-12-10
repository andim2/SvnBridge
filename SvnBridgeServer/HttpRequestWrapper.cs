using System;
using System.Collections.Specialized;
using System.IO;
using System.Web;
using SvnBridge.Interfaces;

namespace SvnBridgeServer
{
    public class HttpRequestWrapper : IHttpRequest
    {
        private readonly HttpRequest request;

        public HttpRequestWrapper(HttpRequest request)
        {
            this.request = request;
        }

        public string ApplicationPath
        {
            get { return request.ApplicationPath; }
        }

        public NameValueCollection Headers
        {
            get { return request.Headers; }
        }

        public string HttpMethod
        {
            get { return request.HttpMethod; }
        }

        public Stream InputStream
        {
            get { return request.InputStream; }
        }

        public Uri Url
        {
            get { return request.Url; }
        }

        public string LocalPath
        {
            get
            {
                return request.AppRelativeCurrentExecutionFilePath.Substring(1);//remove ~
            }
        }
    }
}