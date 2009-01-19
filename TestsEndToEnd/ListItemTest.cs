using System;
using System.Threading;
using Xunit;

namespace EndToEndTests
{
    public class ListItemTest : EndToEndTestBase
    {
        [SvnBridgeFact]
        public void CanListFolderAndFile()
        {
            CreateFolder(testPath + "/TestFolder1", true);
            WriteFile(testPath + "/test.txt", "blah", true);

            string actual = Svn("list " + testUrl);
            string expected = @"TestFolder1/
test.txt
";
            Assert.Equal(expected, actual);
        }

        [SvnBridgeFact]
        public void CanListFolders()
        {
            CreateFolder(testPath + "/TestFolder1", true);
            CreateFolder(testPath + "/TestFolder2", true);

            string actual = Svn("list " + testUrl);
            string expected = @"TestFolder1/
TestFolder2/
";
            Assert.Equal(expected, actual);
        }

        [SvnBridgeFact]
        public void CanListFoldersAndFilesRecursively()
        {
            CreateFolder(testPath + "/TestFolder1", true);
            CreateFolder(testPath + "/TestFolder2", true);
            WriteFile(testPath + "/TestFolder2/text.txt", "blah", true);

            string actual = Svn("list " + testUrl + " --recursive");
            string expected = @"TestFolder1/
TestFolder2/
TestFolder2/text.txt
";
            Assert.Equal(expected, actual);
        }


        [SvnBridgeFact]
        public void CanListPreviousVersion()
        {
            int version = CreateFolder(testPath + "/TestFolder1", true);
            WriteFile(testPath + "/test.txt", "blah", true); // here we create a new version

            string actual = Svn("list " + testUrl + " --revision " + version);
            string expected = @"TestFolder1/
";
            Assert.Equal(expected, actual);
        }


        [SvnBridgeFact]
        public void CanListPreviosuVersion_UsingPrev()
        {
            CreateFolder(testPath + "/TestFolder1", true);
            WriteFile(testPath + "/test.txt", "blah", true); // here we create a new version
            CheckoutAndChangeDirectory();
            WriteFile(testPath + "/test.txt", "foo", true);
            Svn("update");
            string actual = Svn("list test.txt --revision PREV");
            string expected = @"test.txt
";
            Assert.Equal(expected, actual);
        }

        [SvnBridgeFact]
        public void CanListPreviousVersion_WhenDirectoryDoesNotExists()
        {
            CheckoutAndChangeDirectory();
            string actual = SvnExpectError("list --revision PREV");
            string expected = @"svn: Unable to find repository location for '' in revision";
            Assert.True(
                actual.StartsWith(expected)
                );
        }

        [SvnBridgeFact]
        public void CanListPreviousVersionUsingDate()
        {
            CreateFolder(testPath + "/TestFolder1", true);
            DateTime commitDate = DateTime.Now.AddSeconds(1); // Add a second in case server time is slightly off

            //SVN protocol is only accurate to the second
            Thread.Sleep(TimeSpan.FromSeconds(3));

            WriteFile(testPath + "/test.txt", "blah", true); // here we create a new version

            string actual = Svn("list " + testUrl + " --revision {" + commitDate.ToString("yyyyMMddTHHmmss") + "}");
            string expected = "TestFolder1/\r\n";
            Assert.Equal(expected, actual);
        }

        [SvnBridgeFact]
        public void CanListLatestUsingDate()
        {
            CreateFolder(testPath + "/TestFolder1", true);
            WriteFile(testPath + "/test.txt", "blah", true); // here we create a new version

            string actual =
                Svn("list " + testUrl + " --revision {" + DateTime.Now.AddHours(2).ToString("yyyyMMddTHHmmss") + "}");
            string expected = @"TestFolder1/
test.txt
";
            Assert.Equal(expected, actual);
        }

        [SvnBridgeFact]
        public void CanListUsingDateBeforeRepositoryCreated()
        {
            CreateFolder(testPath + "/TestFolder1", true);
            WriteFile(testPath + "/test.txt", "blah", true); // here we create a new version

            string actual =
                SvnExpectError("list " + testUrl + " --revision {" + new DateTime(2000,1,1).ToString("yyyyMMddTHHmmss") + "}");
            Assert.Contains("Unable to find repository location for", actual);
        }

        [SvnBridgeFact]
        public void CanListSingleFolderUsingUrl()
        {
            CreateFolder(testPath + "/TestFolder", true);
            string actual = Svn("list " + testUrl);
            string expected = @"TestFolder/
";
            Assert.Equal(expected, actual);
        }
    }
}
