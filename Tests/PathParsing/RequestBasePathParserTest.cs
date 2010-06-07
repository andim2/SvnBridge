using System;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using Xunit;
using Tests;
using SvnBridge.Infrastructure;
using Attach;
using SvnBridge.PathParsing;

namespace UnitTests
{
    public class RequestBasePathParserTest
    {
        private readonly MyMocks stubs = new MyMocks();

        [Fact]
        public void CanParseServerFromUrl()
        {
            TfsUrlValidator urlValidator = stubs.CreateObject<TfsUrlValidator>(null);
            Results r1 = stubs.Attach((MyMocks.IsValidTfsServerUrl)urlValidator.IsValidTfsServerUrl, Return.Value(true));

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
            Results r1 = stubs.Attach((MyMocks.IsValidTfsServerUrl)urlValidator.IsValidTfsServerUrl, Return.Value(false));

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
            Results r1 = stubs.Attach((MyMocks.IsValidTfsServerUrl)urlValidator.IsValidTfsServerUrl, Return.Value(true));

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
            Results r1 = stubs.Attach((MyMocks.IsValidTfsServerUrl)urlValidator.IsValidTfsServerUrl, Return.Value(true));

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

        [Fact]
        public void CanParseServerFromUrl_WithCollection()
        {
            var urlValidator = stubs.CreateObject<TfsUrlValidator>(null);
            Results r1 = stubs.Attach((MyMocks.IsValidTfsServerUrl)urlValidator.IsValidTfsServerUrl, Return.Value(true));

            var parser = new PathParserServerAndProjectInPath(urlValidator);
            var request = new StubHttpRequest
            {
                Url = new Uri("http://localhost:8081/tfs03.codeplex.com/tfs/DefaultCollection/$/SvnBridge")
            };
            string url = parser.GetServerUrl(request, null);
            Assert.Equal("https://tfs03.codeplex.com/tfs/DefaultCollection", url);
            Assert.Equal("https://tfs03.codeplex.com/tfs/DefaultCollection", r1.Parameters[0]);
        }

        [Fact]
        public void CanParseServerFromUrl_WithCollectionAndPortAndNestedFolder()
        {
            var urlValidator = stubs.CreateObject<TfsUrlValidator>(null);
            Results r1 = stubs.Attach((MyMocks.IsValidTfsServerUrl)urlValidator.IsValidTfsServerUrl, Return.Value(true));

            var parser = new PathParserServerAndProjectInPath(urlValidator);
            var request = new StubHttpRequest
            {
                Url = new Uri("http://localhost:8081/tfs03.codeplex.com:8080/tfs/DefaultCollection/$/SvnBridge/Foo")
            };
            string url = parser.GetServerUrl(request, null);
            Assert.Equal("https://tfs03.codeplex.com:8080/tfs/DefaultCollection", url);
            Assert.Equal("https://tfs03.codeplex.com:8080/tfs/DefaultCollection", r1.Parameters[0]);
        }

        [Fact]
        public void CanGetLocalPath_WithCollection()
        {
            var request = new StubHttpRequest
            {
                Url = new Uri("http://localhost:8081/tfs03.codeplex.com/tfs/DefaultCollection/$/SvnBridge")
            };

            var urlValidator = stubs.CreateObject<TfsUrlValidator>(null);

            var parser = new PathParserServerAndProjectInPath(urlValidator);
            string url = parser.GetLocalPath(request);
            Assert.Equal("/SvnBridge", url);
        }

        [Fact]
        public void CanGetLocalPath_WithCollectionAndPortAndNestedFolder()
        {
            var request = new StubHttpRequest
            {
                Url = new Uri("http://localhost:8081/tfs03.codeplex.com:8080/tfs/DefaultCollection/$/SvnBridge/Foo")
            };

            var urlValidator = stubs.CreateObject<TfsUrlValidator>(null);

            var parser = new PathParserServerAndProjectInPath(urlValidator);
            string url = parser.GetLocalPath(request);
            Assert.Equal("/SvnBridge/Foo", url);
        }
    }
}