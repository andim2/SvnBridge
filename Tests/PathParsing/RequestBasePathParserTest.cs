using System;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.Stubs;
using Xunit;
using Tests;
using SvnBridge.Infrastructure;
using Attach;

namespace SvnBridge.PathParsing
{
    public class RequestBasePathParserTest
    {
        private readonly MyMocks stubs = new MyMocks();

        [Fact]
        public void CanParseServerFromUrl()
        {
            TfsUrlValidator urlValidator = stubs.CreateObject<TfsUrlValidator>(null);
            Results r1 = stubs.Attach(urlValidator.IsValidTfsServerUrl, Return.Value(true));

            PathParserServerAndProjectInPath parser = new PathParserServerAndProjectInPath(urlValidator);
            StubHttpRequest request = new StubHttpRequest
            {
                Url = new Uri("http://localhost:8081/tfs03.codeplex.com/SvnBridge")
            };
            string url = parser.GetServerUrl(request, null);
            Assert.Equal("https://tfs03.codeplex.com", url);
            Assert.Equal("https://tfs03.codeplex.com", r1.Parameters[0]);
        }

        [Fact]
        public void CanParseServerFromUrl_WillUseHttpIfHttpsIsNotValid()
        {
            TfsUrlValidator urlValidator = stubs.CreateObject<TfsUrlValidator>(null);
            Results r1 = stubs.Attach(urlValidator.IsValidTfsServerUrl, Return.Value(false));

            PathParserServerAndProjectInPath parser = new PathParserServerAndProjectInPath(urlValidator);
            IHttpRequest request = new StubHttpRequest
            {
                Url = new Uri("http://localhost:8081/tfs03.codeplex.com/SvnBridge")
            };
            string url = parser.GetServerUrl(request, null);
            Assert.Equal("http://tfs03.codeplex.com", url);
            Assert.Equal("https://tfs03.codeplex.com", r1.History[0].Parameters[0]);
        }


        [Fact]
        public void CanParseServerFromUrl_WithPort()
        {
            TfsUrlValidator urlValidator = stubs.CreateObject<TfsUrlValidator>(null);
            Results r1 = stubs.Attach(urlValidator.IsValidTfsServerUrl, Return.Value(true));

            PathParserServerAndProjectInPath parser = new PathParserServerAndProjectInPath(urlValidator);
            IHttpRequest request = new StubHttpRequest
            {
                Url = new Uri("http://localhost:8081/tfs03.codeplex.com:8080/SvnBridge")
            };
            string url = parser.GetServerUrl(request, null);
            Assert.Equal("https://tfs03.codeplex.com:8080", url);
            Assert.Equal("https://tfs03.codeplex.com:8080", r1.Parameters[0]);
        }


        [Fact]
        public void CanParseServerFromUrl_WithPortAndNestedFolder()
        {
            TfsUrlValidator urlValidator = stubs.CreateObject<TfsUrlValidator>(null);
            Results r1 = stubs.Attach(urlValidator.IsValidTfsServerUrl, Return.Value(true));

            PathParserServerAndProjectInPath parser = new PathParserServerAndProjectInPath(urlValidator);
            IHttpRequest request = new StubHttpRequest
            {
                Url = new Uri("http://localhost:8081/tfs03.codeplex.com:8080/SvnBridge/Foo")
            };
            string url = parser.GetServerUrl(request,null);
            Assert.Equal("https://tfs03.codeplex.com:8080", url);
            Assert.Equal("https://tfs03.codeplex.com:8080", r1.Parameters[0]);
        }

        [Fact]
        public void CanGetLocalPath_WithoutServerUrl()
        {
            StubHttpRequest request = new StubHttpRequest();
            request.Url = new Uri("http://localhost:8081/tfs03.codeplex.com:8080/SvnBridge");

            TfsUrlValidator urlValidator = stubs.CreateObject<TfsUrlValidator>(null);

            PathParserServerAndProjectInPath parser = new PathParserServerAndProjectInPath(urlValidator);
            string url = parser.GetLocalPath(request);
            Assert.Equal("/SvnBridge", url);

        }
    }
}