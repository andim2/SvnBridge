using System.IO;
using Xunit;

namespace TestsEndToEnd
{
    public class CopyTest : EndToEndTestBase
    {
        [SvnBridgeFact]
        public void CanTagItem()
        {
            CreateFolder(testPath + "/trunk", false);
            WriteFile(testPath + "/trunk/file.txt", "abc", false);
            CreateFolder(testPath + "/tags", true);
            CheckoutAndChangeDirectory();

            Svn("copy " + testUrl + "/trunk " + testUrl + "/tags/test1 -m tagging");
            Svn("up");
            Assert.Equal("abc", File.ReadAllText("tags/test1/file.txt"));
        }
    }
}