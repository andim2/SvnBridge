using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using SvnBridge.Handlers;
using SvnBridge.Interfaces;
using System.Net;
using SvnBridge.Stubs;
using System.Collections.Specialized;
using SvnBridge.Infrastructure.Statistics;
using SvnBridge.SourceControl;

namespace SvnBridge.Net
{
    public class HttpContextDispatcherTests : IDisposable
    {
        public HttpContextDispatcherTests()
        {
            RequestCache.Init();
        }

        public void Dispose()
        {
            RequestCache.Dispose();
        }

        [Fact]
        public void Dispatch_WhenServerIsCodePlexAndUsernameIsMissingDomainAndSuffix_DomainAndSuffixIsAdded()
        {
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();
            StubHttpContext context = new StubHttpContext();
            StubHttpRequest request = new StubHttpRequest();
            context.Request = request;
            request.Url = new Uri("https://tfs01.codeplex.com"); ;
            request.Headers = new NameValueCollection();
            request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes("username:password"));
            
            dispatcher.Dispatch(context);
            
            Assert.Equal(@"username_cp", dispatcher.Handler.Handle_credentials.UserName);
            Assert.Equal(@"snd", dispatcher.Handler.Handle_credentials.Domain);
        }

        [Fact]
        public void Dispatch_WhenServerIsCodePlexAndNoAuthorizationHeader_CredentialsAreNull()
        {
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();
            StubHttpContext context = new StubHttpContext();
            StubHttpRequest request = new StubHttpRequest();
            context.Request = request;
            request.Url = new Uri("https://tfs01.codeplex.com"); ;
            request.Headers = new NameValueCollection();

            dispatcher.Dispatch(context);

            Assert.Null(dispatcher.Handler.Handle_credentials);
        }
    }

    public class TestableHttpContextDispatcher : HttpContextDispatcher
    {
        public StubHandler Handler = new StubHandler();

        public TestableHttpContextDispatcher() : base(new StubParser(), new StubActionTracking()) { }

        public override RequestHandlerBase GetHandler(string httpMethod)
        {
            return Handler;
        }
    }

    public class StubActionTracking : ActionTrackingViaPerfCounter
    {
        public override void Request(RequestHandlerBase handler) {}
        public override void Error() {}
        public override IDictionary<string, long> GetStatistics()
        {
            return null;
        }
    }

    public class StubParser : IPathParser
    {
        public string GetServerUrl(IHttpRequest request, ICredentials credentials)
        {
            return "https://tfs01.codeplex.com";
        }

        public string GetLocalPath(IHttpRequest request)
        {
            throw new NotImplementedException();
        }

        public string GetLocalPath(IHttpRequest request, string url)
        {
            throw new NotImplementedException();
        }

        public string GetProjectName(IHttpRequest request)
        {
            return "testproject";
        }

        public string GetApplicationPath(IHttpRequest request)
        {
            throw new NotImplementedException();
        }

        public string GetActivityId(string href)
        {
            throw new NotImplementedException();
        }

        public string GetActivityIdFromDestination(string href)
        {
            throw new NotImplementedException();
        }

        public string ToApplicationPath(IHttpRequest request, string href)
        {
            throw new NotImplementedException();
        }

        public string GetPathFromDestination(string href)
        {
            throw new NotImplementedException();
        }
    }

    public class StubHandler : RequestHandlerBase
    {
        public NetworkCredential Handle_credentials;

        public override void Handle(IHttpContext context, IPathParser pathParser, NetworkCredential credentials)
        {
            Handle_credentials = credentials;
        }

        protected override void Handle(IHttpContext context, TFSSourceControlProvider sourceControlProvider)
        {
        }
    }
}
