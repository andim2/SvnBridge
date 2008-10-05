using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using SvnBridge.SourceControl;
using SvnBridge.PathParsing;
using SvnBridge.Stubs;

namespace IntegrationTests
{
    public class PathParserProjectInDomainCodePlexTest
    {
        [Fact]
        public void GetProjectName_ProjectHasBeenRenamed_ReturnsOriginalProjectName()
        {
            PathParserProjectInDomainCodePlex pathParser = new PathParserProjectInDomainCodePlex();
            StubHttpRequest request = new StubHttpRequest();
            request.Headers["Host"] = "subsonic.svn.codeplex.com";
            string result = pathParser.GetProjectName(request);

            Assert.Equal("actionpack", result);
        }

        [Fact]
        public void GetServerUrl_ReturnsCorrectServerNameUsingCodePlexService()
        {
            PathParserProjectInDomainCodePlex pathParser = new PathParserProjectInDomainCodePlex();
            StubHttpRequest request = new StubHttpRequest();
            request.Headers["Host"] = "svnbridge.svn.codeplex.com";
            string result = pathParser.GetServerUrl(request, null);

            Assert.Equal("https://tfs03.codeplex.com", result);
        }
    }
}
