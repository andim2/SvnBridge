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
using SvnBridge.PathParsing;
using SvnBridge.Infrastructure;
using System.IO;
using System.Web;

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

        //[Fact]
        //public void Repro()
        //{
        //    BootStrapper.Start();
        //    IPathParser pathParser = new PathParserProjectInDomain("http://codeplex-team:8080", Container.Resolve<TFSSourceControlService>());
        //    HttpContextDispatcher dispatcher = new HttpContextDispatcher(pathParser, Container.Resolve<ActionTrackingViaPerfCounter>());

        //    StubHttpContext context = new StubHttpContext();
        //    StubHttpRequest request = new StubHttpRequest();
        //    StubHttpResponse response = new StubHttpResponse();
        //    context.Request = request;
        //    context.Response = response;
        //    response.OutputStream = new MemoryStream(Constants.BufferSize);

        //    RequestCache.Init();
        //    request.ApplicationPath = "/svn";
        //    request.HttpMethod = "REPORT";
        //    request.Path = "http://galleries.redmond.corp.microsoft.com/svn/!svn/vcc/default";
        //    request.Input =
        //        //"<?xml version=\"1.0\" encoding=\"utf-16\"?>" +
        //        "<update-report xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" send-all=\"true\" xmlns=\"svn:\">" +
        //        "  <entry rev=\"6629\" start-empty=\"true\" />" +
        //        "  <src-path>http://galleries.redmond.corp.microsoft.com/svn</src-path>" +
        //        "  <target-revision>6629</target-revision>" +
        //        "</update-report>";

        //    dispatcher.Dispatch(context);

        //    string output = Encoding.Default.GetString(((MemoryStream)response.OutputStream).ToArray());
        //    System.Diagnostics.Debug.WriteLine(output);
        //}

        [Fact]
        public void Dispatch_ServerIsCodePlexAndUsernameIsMissingDomainAndSuffix_DomainAndSuffixIsAdded()
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
        public void Dispatch_ServerIsCodePlexAndNoAuthorizationHeader_CredentialsAreNull()
        {
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();
            StubHttpContext context = new StubHttpContext();
            StubHttpRequest request = new StubHttpRequest();
            context.Request = request;
            request.Url = new Uri("https://tfs01.codeplex.com"); ;

            dispatcher.Dispatch(context);

            Assert.Null(dispatcher.Handler.Handle_credentials);
        }

        [Fact]
        public void Dispatch_RequestIsCancelledUnderIIS6_ExceptionIsNotThrown()
        {
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();
            StubHttpContext context = new StubHttpContext();
            StubHttpRequest request = new StubHttpRequest();
            context.Request = request;
            request.Url = new Uri("https://tfs01.codeplex.com"); ;
            dispatcher.Handler.Handle_Throw = new IOException();

            Exception result = Record.Exception(delegate { dispatcher.Dispatch(context); });

            Assert.Null(result);
        }

        [Fact]
        public void Dispatch_RequestIsCancelledUnderIIS7_ExceptionIsNotThrown()
        {
            TestableHttpContextDispatcher dispatcher = new TestableHttpContextDispatcher();
            StubHttpContext context = new StubHttpContext();
            StubHttpRequest request = new StubHttpRequest();
            context.Request = request;
            request.Url = new Uri("https://tfs01.codeplex.com"); ;
            dispatcher.Handler.Handle_Throw = new HttpException("The remote host closed the connection.");

            Exception result = Record.Exception(delegate { dispatcher.Dispatch(context); });

            Assert.Null(result);
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
        public Exception Handle_Throw = null;

        public override void Handle(IHttpContext context, IPathParser pathParser, NetworkCredential credentials)
        {
            Handle_credentials = credentials;
            if (Handle_Throw != null)
                throw Handle_Throw;
        }

        protected override void Handle(IHttpContext context, TFSSourceControlProvider sourceControlProvider)
        {
        }
    }
}
