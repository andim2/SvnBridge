using System;
using System.Reflection;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.SourceControl;
using Tests;
using Attach;
using SvnBridge.Infrastructure;
using CodePlex.TfsLibrary.ObjectModel;
using System.Windows.Forms;
using SvnBridge.PathParsing;

namespace UnitTests
{
    public class PathParserProjectInDomainTest : IDisposable
	{
        protected MyMocks stubs = new MyMocks();

        public void Dispose()
        {
            PathParserProjectInDomain.ResetCache();
        }
        
        [Fact]
        public void PathParserProjectInDomain_DoesNotAcceptInvalidUrl()
		{
            Exception result = Record.Exception(delegate {
                new PathParserProjectInDomain("blah", new StubTFSSourceControlService());
            });

            Assert.NotNull(result);
		}

        [Fact]
        public void PathParserProjectInDomain_AcceptValidUrl()
        {
            Exception result = Record.Exception(delegate
            {
                new PathParserProjectInDomain("https://codeplex.com", new StubTFSSourceControlService());
            });

            Assert.Null(result);
        }

        [Fact]
        public void PathParserProjectInDomain_AcceptValidUrl_Muliple()
        {
            Exception result = Record.Exception(delegate
            {
                new PathParserProjectInDomain("https://codeplex.com,https://www.codeplex.com", new StubTFSSourceControlService());
            });

            Assert.Null(result);
        }

        [Fact]
        public void GetProjectName_ReturnsRemoteName()
        {
            StubTFSSourceControlService sourceControlService = new StubTFSSourceControlService();
            SourceItem sourceItem = new SourceItem();
            sourceItem.RemoteName = "$/ProjectName";
            sourceControlService.QueryItems_Return.Add(sourceItem);
            PathParserProjectInDomain parser = new PathParserProjectInDomain("https://tfs01.codeplex.com", sourceControlService);
            StubHttpRequest request = new StubHttpRequest();
            request.Headers["Host"] = "projectname.codeplex.com";
            parser.GetServerUrl(request, null);

            string result = parser.GetProjectName(request);

            Assert.Equal("ProjectName", result);
        }

        [Fact]
        public void GetServerUrl_MultipleTFSServersAreConfigured_ReturnsCorrectServerForProject()
        {
            StubTFSSourceControlService sourceControlService = new StubTFSSourceControlService();
            sourceControlService.QueryItems_ReturnDelegate = delegate(object[] parameters)
            {
                if ((string)parameters[0] == "https://tfs02.codeplex.com")
                {
                    SourceItem sourceItem = new SourceItem();
                    sourceItem.RemoteName = "$/ProjectName";
                    return new SourceItem[] { sourceItem };
                }
                return new SourceItem[] {};
            };
            PathParserProjectInDomain parser = new PathParserProjectInDomain("https://tfs01.codeplex.com,https://tfs02.codeplex.com", sourceControlService);
            StubHttpRequest request = new StubHttpRequest();
            request.Headers["Host"] = "projectname.codeplex.com";

            string result = parser.GetServerUrl(request, null);

            Assert.Equal("https://tfs02.codeplex.com", result);
        }

        [Fact]
        public void GetServerUrl_ProjectNotFoundOnServer_ThrowsException()
        {
            StubTFSSourceControlService sourceControlService = new StubTFSSourceControlService();
            PathParserProjectInDomain parser = new PathParserProjectInDomain("https://tfs01.codeplex.com", sourceControlService);
            StubHttpRequest request = new StubHttpRequest();
            request.Headers["Host"] = "projectname.codeplex.com";

            Exception result = Record.Exception(delegate { parser.GetServerUrl(request, null); });

            Assert.IsType(typeof(InvalidOperationException), result);
            Assert.Equal("Could not find project 'projectname' in: https://tfs01.codeplex.com", result.Message);
        }

        [Fact]
        public void GetServerUrl_CalledMoreThenOnce_ReturnsResultFromCache()
        {
            StubTFSSourceControlService sourceControlService = new StubTFSSourceControlService();
            SourceItem sourceItem = new SourceItem();
            sourceItem.RemoteName = "$/ProjectName";
            sourceControlService.QueryItems_Return.Add(sourceItem);
            PathParserProjectInDomain parser = new PathParserProjectInDomain("https://tfs01.codeplex.com", sourceControlService);
            StubHttpRequest request = new StubHttpRequest();
            request.Headers["Host"] = "projectname.codeplex.com";
            parser.GetServerUrl(request, null);
            sourceControlService.QueryItems_ReturnDelegate = delegate(object[] parameters) { throw new Exception("Called source control service"); };

            string result = parser.GetServerUrl(request, null);

            Assert.Equal("https://tfs01.codeplex.com", result);
        }
    }
}