using Xunit;

namespace TestsEndToEnd
{
    public class InfoTest : EndToEndTestBase
    {
        [SvnBridgeFact]
        public void InfoOnRootFolder()
        {
            string result = Svn("info " + testUrl);

            Assert.True(result.Contains("Revision: " + _lastCommitRevision));
            Assert.True(result.Contains("Last Changed Rev: " + _lastCommitRevision));
        }

        [SvnBridgeFact]
        public void InfoOnRootFolderWithUpdatesReturnsLatestRevision()
        {
            WriteFile(testPath + "/file.txt", "abc", true);

            string result = Svn("info " + testUrl);

            Assert.True(result.Contains("Last Changed Rev: " + _lastCommitRevision));
        }
    }
}
