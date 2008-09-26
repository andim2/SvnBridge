using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using SvnBridge.SourceControl;

namespace IntegrationTests
{
    public class ProjectInformationRepositoryTest
    {
        [Fact]
        public void GetProjectInformation_UseCodePlexServersFlagSetToTrue_ReturnsInformationBasedOnCodePlexWebService()
        {
            ProjectInformationRepository repository = new ProjectInformationRepository(null, null, true);

            ProjectLocationInformation info = repository.GetProjectLocation(null, "subsonic");

            Assert.Equal("actionpack", info.RemoteProjectName);
            Assert.Equal("https://tfs01.codeplex.com", info.ServerUrl);
        }
    }
}
