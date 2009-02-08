using System;
using System.IO;
using Xunit;

namespace EndToEndTests
{
    public class CommitTest : EndToEndTestBase
    {
        [SvnBridgeFact]
        public void CanCommitNewFile()
        {
            CheckoutAndChangeDirectory();
            File.WriteAllText("test.txt", "hab");
            Svn("add test.txt");
            string command = Svn("commit -m blah");
            Assert.True(
                command.Contains("Committed")
                );
        }

        [SvnBridgeFact]
        public void CanCommitBigFile()
        {
            CheckoutAndChangeDirectory();
            GenerateFile();
            string originalPath = Path.GetFullPath("test.txt");
            Svn("add test.txt");
            Svn("commit -m \"big file\" ");

            CheckoutAgainAndChangeDirectory();
            string newPath = Path.GetFullPath("test.txt");
            string actual = File.ReadAllText(newPath);
            string expected = File.ReadAllText(originalPath);
            Assert.Equal(expected, actual);
        }

		[SvnBridgeFact]
		public void CanCopyFile()
		{
			CheckoutAndChangeDirectory();
			File.WriteAllText("test.txt", "blah");
			Svn("add test.txt");
			Svn("commit -m \"big file\" ");
			Svn("copy test.txt test2.txt");
			Svn("commit -m copy");
		}

		[SvnBridgeFact]
		public void CanRenameFile()
		{
			CheckoutAndChangeDirectory();
			File.WriteAllText("test.txt", "blah");
			Svn("add test.txt");
			Svn("commit -m \"big file\" ");
			Svn("ren test.txt test2.txt");
			Svn("commit -m copy");
		}

		[SvnBridgeFact]
		public void CanRenameAndEditFile()
		{
			CheckoutAndChangeDirectory();
			File.WriteAllText("test.txt", "blah");
			Svn("add test.txt");
			Svn("commit -m \"big file\" ");
			Svn("ren test.txt test2.txt");
			File.WriteAllText("test.txt", "blah2");
			Svn("commit -m copy");
		}

		[SvnBridgeFact]
		public void CanEditAndRenameFile()
		{
			CheckoutAndChangeDirectory();
			File.WriteAllText("test.txt", "blah");
			Svn("add test.txt");
			Svn("commit -m \"big file\" ");
			File.WriteAllText("test.txt", "blah2");
			Svn("ren --force test.txt test2.txt");
			File.WriteAllText("test.txt", "blah3");
			Svn("commit -m copy");
		}

		[SvnBridgeFact]
		public void CanCopyThenDeleteFile()
		{
			CheckoutAndChangeDirectory();
			File.WriteAllText("test.txt", "blah");
			Svn("add test.txt");
			Svn("commit -m \"big file\" ");
			Svn("copy test.txt test2.txt");
			Svn("del test.txt");
			Svn("commit -m copy");
		}

		[SvnBridgeFact]
		public void CanCopyEditThenDeleteFile()
		{
			CheckoutAndChangeDirectory();
			File.WriteAllText("test.txt", "blah");
			Svn("add test.txt");
			Svn("commit -m \"big file\" ");
			Svn("copy test.txt test2.txt");
			File.WriteAllText("test2.txt", "blah2");
			Svn("del test.txt");
			Svn("commit -m copy");
		}

        [SvnBridgeFact]
        public void CanRenameFileThenRenameAnotherFileToOriginalNameOfFirstFile()
        {
            CheckoutAndChangeDirectory();
            File.WriteAllText("test1.txt", "blah");
            File.WriteAllText("test2.txt", "blah");
            Svn("add test1.txt");
            Svn("add test2.txt");
            Svn("commit -m test");
            Svn("update");
            Svn("rename test2.txt test3.txt");
            Svn("rename test1.txt test2.txt");
            Svn("commit -m copy");
        }

        private static void GenerateFile()
        {
            int lines = 1024 * 10;
            using (TextWriter writer = File.CreateText("test.txt"))
            {
                for (int i = 0; i < lines; i++)
                {
					int lineWidth = 128;
                    string [] items = new string[lineWidth];
                    for (int j = 0; j < lineWidth; j++)
                    {
                        items[j] = (j*i).ToString();
                    }
                    writer.WriteLine(string.Join(", ", items));
                }
                writer.Flush();
            }
        }
    }
}
