using Xunit;

namespace TestsEndToEnd
{
    public class LogTest : EndToEndTestBase
    {
        [SvnBridgeFact]
        public void CanAskForLogOfItemThatDoesNotExists()
        {
            string command = SvnExpectError("log " + testUrl + " --revision 1");
            Assert.Equal("svn: Unable to find repository location for '"  + testUrl 
                            + "' in revision 1\r\n",
                            command);
        }

        [SvnBridgeFact]
        public void CanGetLogByUrl()
        {
            int revision = CreateFolder(testPath + "/Test4", true);
            string command = Svn("log " + testUrl + " --revision " + revision);
            Assert.True(
                command.Contains("r" + revision), "does not contains revision"
                );
            Assert.True(
                command.Contains("A /SvnBridgeTesting" + testPath + "/Test4"),
                "does not contains created folder"
                );
        }

        [SvnBridgeFact]
        public void CanGetLogOfAllChanges_WithoutLimit()
        {
            CheckoutAndChangeDirectory();

            string actual = Svn("log");
            // we want to verify that we can execute it, not verify the contet
            Assert.False(string.IsNullOrEmpty(actual));
        }
    }
}
