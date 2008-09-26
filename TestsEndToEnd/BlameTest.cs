using System;
using System.Collections.Generic;
using Xunit;

namespace TestsEndToEnd
{
    public class BlameTest : EndToEndTestBase
    {
        [SvnBridgeFact]
        public void CannotBlameOnFolder()
        {
            CreateFolder(testPath + "/TestFolder1", true);
            CheckoutAndChangeDirectory();
            string error = SvnExpectError("blame TestFolder1");
            Assert.Contains("TestFolder1' is not a file", error);
        }

        [SvnBridgeFact]
        public void CannotBlameOnNonExistingFile()
        {
            CreateFolder(testPath + "/TestFolder1", true);
            CheckoutAndChangeDirectory();
            string error = SvnExpectError("blame " + testUrl + "/not_here");
            Assert.Contains("not_here' path not found", error);
        }

        [SvnBridgeFact]
        public void CanGetBlameResultsFromFile()
        {
            WriteFile(testPath + "/foo.txt", @"a
", true);
            WriteFile(testPath + "/foo.txt", @"a
b
", true);
            WriteFile(testPath + "/foo.txt", @"a
b
c
", true);
            WriteFile(testPath + "/foo.txt", @"a
b
c
d
", true);
            CheckoutAndChangeDirectory();
            string blame = Svn("blame foo.txt");
            List<BlameInfo> blames = new List<BlameInfo>();
            foreach (string line in blame.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                blames.Add(BlameInfo.Parse(line));
            }
            Assert.Equal(4, blames.Count);

            Assert.Equal(_lastCommitRevision - 3, blames[0].Revision);
            Assert.Equal(_lastCommitRevision - 2, blames[1].Revision);
            Assert.Equal(_lastCommitRevision - 1, blames[2].Revision);
            Assert.Equal(_lastCommitRevision, blames[3].Revision);

            foreach (BlameInfo info in blames)
            {
                Assert.Contains(Environment.UserName, info.Username);
            }

            Assert.Equal("a", blames[0].Line);
            Assert.Equal("b", blames[1].Line);
            Assert.Equal("c", blames[2].Line);
            Assert.Equal("d", blames[3].Line);
        
        }

        public class BlameInfo
        {
            public int Revision;
            public string Username;
            public string Line;

            public static BlameInfo Parse(string line)
            {
                string[] parts = line.Split(new char[]{' '},StringSplitOptions.RemoveEmptyEntries);
                BlameInfo info = new BlameInfo();
                info.Revision = int.Parse(parts[0]);
                info.Username = parts[1];
                info.Line = parts[2];
                return info;
            }
        }
    }
}