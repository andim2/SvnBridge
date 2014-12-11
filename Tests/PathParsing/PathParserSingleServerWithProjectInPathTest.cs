using System;
using System.Collections.Generic;
using System.Text;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Net;
using SvnBridge.SourceControl;
using Tests;
using SvnBridge.PathParsing;

namespace UnitTests
{
    public class PathParserSingleServerWithProjectInPathTest
    {
        protected MyMocks stubs = new MyMocks();

        [Fact]
        public void PathParserSingleServerWithProjectInPath_DoesNotAcceptInvalidUrl()
        {
            Exception result = Record.Exception(delegate
            {
                new PathParserSingleServerWithProjectInPath("blah");
            });

            Assert.NotNull(result);
        }

        [Fact]
        public void PathParserSingleServerWithProjectInPath_AcceptValidUrl()
        {
            Exception result = Record.Exception(delegate
            {
                new PathParserSingleServerWithProjectInPath("https://codeplex.com");
            });

            Assert.Null(result);
        }

        [Fact]
        public void GetLocalPath_PathIsRoot_ReturnsRootPath()
        {
            PathParserSingleServerWithProjectInPath parser = new PathParserSingleServerWithProjectInPath("http://www.codeplex.com");
            StubHttpRequest request = new StubHttpRequest();

            string result = parser.GetLocalPath(request, "http://www.root.com");

            Assert.Equal("/", result);
        }

        [Fact]
        public void GetLocalPath_PathIsApplicationRoot_ReturnsRootPath()
        {
            PathParserSingleServerWithProjectInPath parser = new PathParserSingleServerWithProjectInPath("http://www.codeplex.com");
            StubHttpRequest request = new StubHttpRequest();
            request.ApplicationPath = "/svn";

            string result = parser.GetLocalPath(request, "http://www.root.com/svn");

            Assert.Equal("/", result);
        }

        [Fact]
        public void Test()
        {
            PathParserSingleServerWithProjectInPath parser = new PathParserSingleServerWithProjectInPath("http://svnbridgetesting.redmond.corp.microsoft.com");

            string result = parser.GetPathFromDestination("http://svnbridgetesting.redmond.corp.microsoft.com/svn/!svn/wrk/6874f51f-0540-b24f-bbd8-eac3072c5a51/Test/Test2.txt");

            Assert.Equal("/!svn/wrk/6874f51f-0540-b24f-bbd8-eac3072c5a51/Test/Test2.txt", result);
        }
    }
}
