using System.Collections.Specialized;
using System.IO;
using Xunit;
using SvnBridge.SourceControl;
using SvnBridge.Stubs;
using Tests;
using SvnBridge.Net;
using Attach;
using System;

namespace SvnBridge.Infrastructure
{
    public abstract class HandlerTestsBase : IDisposable
    {
        protected StubHttpContext context;
        protected StubHttpRequest request;
        protected StubHttpResponse response;
        protected TFSSourceControlProvider provider;
        protected MyMocks stubs = new MyMocks();
        protected string tfsUrl;

        public HandlerTestsBase()
        {
            BootStrapper.Start();
            provider = stubs.CreateTFSSourceControlProviderStub();
            Container.Register(typeof(TFSSourceControlProvider), provider);
            context = new StubHttpContext();
            request = new StubHttpRequest();
            request.Headers = new NameValueCollection();
            context.Request = request;
            response = new StubHttpResponse();
            response.OutputStream = new MemoryStream(Constants.BufferSize);
            context.Response = response;
            tfsUrl = "http://tfsserver";
            RequestCache.Init();
        }

        public void Dispose()
        {
            Clock.FrozenCurrentTime = null;
            Container.Reset();
        }
    }
}