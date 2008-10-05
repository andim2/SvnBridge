using System;
using System.Reflection;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.SourceControl;
using Tests;
using SvnBridge.Stubs;
using Attach;

namespace SvnBridge.PathParsing
{
    public class PathParserProjectInDomainTest
	{
        protected MyMocks stubs = new MyMocks();
        
        [Fact]
        public void PathParserProjectInDomain_DoesNotAcceptInvalidUrl()
		{
            Exception result = Record.Exception(delegate {
                new PathParserProjectInDomain("blah", stubs.CreateProjectInformationRepositoryStub());
            });

            Assert.NotNull(result);
		}

        [Fact]
        public void PathParserProjectInDomain_AcceptValidUrl()
        {
            Exception result = Record.Exception(delegate
            {
                new PathParserProjectInDomain("https://codeplex.com", stubs.CreateProjectInformationRepositoryStub());
            });

            Assert.Null(result);
        }

        [Fact]
        public void PathParserProjectInDomain_AcceptValidUrl_Muliple()
        {
            Exception result = Record.Exception(delegate
            {
                new PathParserProjectInDomain("https://codeplex.com,https://www.codeplex.com", stubs.CreateProjectInformationRepositoryStub());
            });

            Assert.Null(result);
        }

        [Fact]
        public void GetProjectName_ReturnsRemoteName()
        {
            ProjectInformationRepository projectInformationRepository = stubs.CreateProjectInformationRepositoryStub();
            ProjectLocationInformation projectLocationInformation = new ProjectLocationInformation("ProjectName", null);
            stubs.Attach((MyMocks.GetProjectLocation)projectInformationRepository.GetProjectLocation, projectLocationInformation);
            PathParserProjectInDomain parser = new PathParserProjectInDomain("https://codeplex.com,https://www.codeplex.com", projectInformationRepository);
            StubHttpRequest request = new StubHttpRequest();
            request.Headers["Host"] = "projectname.codeplex.com";

            string result = parser.GetProjectName(request);

            Assert.Equal("ProjectName", result);
        }
	}
}