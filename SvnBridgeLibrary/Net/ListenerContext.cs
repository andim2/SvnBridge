using System.IO;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;

namespace SvnBridge.Net
{
    public sealed class ListenerContext : IHttpContext
    {
        private readonly ListenerRequest request;
        private readonly ListenerResponse response;

        public ListenerContext(Stream stream, DefaultLogger logger)
        {
            request = new ListenerRequest(stream, logger);
            response = new ListenerResponse(stream);
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
