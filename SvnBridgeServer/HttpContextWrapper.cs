using System.Web;
using SvnBridge.Interfaces;

namespace SvnBridgeServer
{
    public class HttpContextWrapper : IHttpContext
    {
        private readonly IHttpRequest request;
        private readonly IHttpResponse response;

        public HttpContextWrapper(HttpContext context)
        {
            request = new HttpRequestWrapper(context.Request);
            response = new HttpResponseWrapper(context.Response);
        }

        #region IHttpContext Members

        public IHttpRequest Request
        {
            get { return request; }
        }

        public IHttpResponse Response
        {
            get { return response; }
        }

        #endregion
    }
}