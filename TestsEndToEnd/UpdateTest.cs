using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Xunit;

namespace TestsEndToEnd
{
    public class UpdateTest : EndToEndTestBase
    {
		[SvnBridgeFact]
		public void Update_FileWasRemovedAndAnotherAddedWithSameName_FileHasCorrectContents()
		{
			WriteFile(testPath + "/file", "blah1", true);

			CheckoutAndChangeDirectory();
	
			DeleteItem(testPath + "/file", true);
			WriteFile(testPath + "/file", "blah2", true);

			Svn("up");

			Assert.Equal("blah2", File.ReadAllText("file"));
		}

        [SvnBridgeFact]
        public void Update_FileClientStateIsDifferentAndFileWasModified_FileHasCorrectContents()
        {
            WriteFile(testPath + "/test.txt", "blah1", true);
            CheckoutAndChangeDirectory();
            File.WriteAllText("test.txt", "blah2");
            Svn("commit -m edit");
            WriteFile(testPath + "/test.txt", "blah3", true);

            Svn("up");

            Assert.Equal("blah3", File.ReadAllText("test.txt"));
        }

        [SvnBridgeFact]
		public void CanUpdateWorkingCopy_AfterRenameFromOfFileFromOneFolderToAnother_WhenUpdatingFromTheOriginalFolder()
		{
			CreateFolder(testPath + "/src", true);
			CreateFolder(testPath + "/dest", true);
			WriteFile(testPath + "/src/file", "blah", true);

			CheckoutAndChangeDirectory();

			Assert.True(File.Exists("src/file"));

			RenameItem(testPath + "/src/file", testPath + "/dest/file", true);

			Svn("up src/file");

			Assert.False(File.Exists("src/file"));
		}

		[SvnBridgeFact]
		public void Updating_Directory_With_Files_Using_Different_Versions_Than_Parent_Dir()
		{
			CreateFolder(testPath + "/src", true);
			CreateFolder(testPath + "/src/foo", true);
			WriteFile(testPath + "/src/foo/bar", "blah3", true);

			CheckoutAndChangeDirectory();
			WriteFile(testPath + "/src/foo/bar", "blah2", true);
			Svn("up src/foo/bar -r PREV");

			WriteFile(testPath + "/src/foo/bar", "blah1", true);

			Svn("up src/foo");

			Assert.Equal("blah1", File.ReadAllText("src/foo/bar"));
		}

		[SvnBridgeFact]
		public void CanUpdateWorkingCopy_AfterRenameFromOfFileFromOneFolderToAnother_WhenUpdatingFromTheDestFolder()
		{
			CreateFolder(testPath + "/src", true);
			CreateFolder(testPath + "/dest", true);
			WriteFile(testPath + "/src/file", "blah", true);

			CheckoutAndChangeDirectory();

			Assert.True(File.Exists("src/file"));

			RenameItem(testPath + "/src/file", testPath + "/dest/file", true);

			Svn("up dest");

			Assert.True(File.Exists("dest/file"));
		}


        [SvnBridgeFact]
        public void CanUpdateWorkingCopy()
        {
            CheckoutAndChangeDirectory();
            File.WriteAllText("test.txt", "hab");
            Svn("commit -m blah");
            WriteFile(testPath + "/test2.txt", "blah", true);
            string output = Svn("update");
            Assert.True(
                output.Contains("A    test2.txt")
                );
        }

        [SvnBridgeFact]
        public void AddFiles_Remove_AndRenameParent()
        {
            CheckoutAndChangeDirectory();

            CreateFolder(testPath + "/Foo", true);
            CreateFolder(testPath + "/Foo/obj", true);
            WriteFile(testPath + "/Foo/obj/b.txt", "b", true);
            DeleteItem(testPath + "/Foo/obj", true);
            RenameItem(testPath + "/Foo", testPath + "/Bar", true);

            Svn("update");
        }

        [SvnBridgeFact]
        public void AddFiles_Remove_AndRenameParent_Then_Backward()
        {
            CheckoutAndChangeDirectory();

            CreateFolder(testPath + "/Foo", true);
            CreateFolder(testPath + "/Foo/obj", true);
            WriteFile(testPath + "/Foo/obj/b.txt", "b", true);
            DeleteItem(testPath + "/Foo/obj", true);
            int revision = _lastCommitRevision;
            RenameItem(testPath + "/Foo", testPath + "/Bar", true);

            Svn("update");

            Svn("update -r " + revision);
        }

        [SvnBridgeFact]
        public void AddFiles_Remove_AndRenameParent_WhenRootFolderHasSeveralSubFolder()
        {
            CheckoutAndChangeDirectory();

            CreateFolder(testPath + "/Foo", true);
            CreateFolder(testPath + "/Foo/obj", true);
            CreateFolder(testPath + "/Foo/U", true);
            WriteFile(testPath + "/Foo/U/c.txt", "b", true);
            CreateFolder(testPath + "/Foo/Y", true);
            WriteFile(testPath + "/Foo/Y/D.txt", "b", true);
            WriteFile(testPath + "/Foo/obj/b.txt", "b", true);
            DeleteItem(testPath + "/Foo/obj", true);
            RenameItem(testPath + "/Foo", testPath + "/Bar", true);

            Svn("update");
        }

        [SvnBridgeFact]
        public void CanUpdateWorkingCopyToPreviousVersion()
        {
            CheckoutAndChangeDirectory();
            File.WriteAllText("test.txt", "hab");
            Svn("add test.txt");
            Svn("commit -m blah");
            Svn("update");
            Svn("update test.txt --revision PREV");
        }

        [SvnBridgeFact]
        public void CanUpdateWorkingCopyToPreviousVersion_AndRemoveFolder()
        {
            CheckoutAndChangeDirectory();
            Directory.CreateDirectory("foo");
            File.WriteAllText("foo/test.txt", "hab");
            Svn("add foo");
            Svn("commit -m blah");
            Svn("update");
            Svn("update foo --revision PREV");
        }

        [SvnBridgeFact]
        public void RemoveFolderLocallyThenRemoveFileInFolderAtServerWillNotRetreiveFile()
        {
            CheckoutAndChangeDirectory();
            Directory.CreateDirectory("foo");
            File.WriteAllText("foo/test.txt", "hab");
            Svn("add foo");
            Svn("commit -m blah");
            Svn("update");

            RmDir("foo");
            Assert.False(File.Exists("foo/test.txt"));

            DeleteItem(testPath + "/foo/test.txt", true);
            string svn = Svn("update");
            Assert.Contains("A    foo", svn);
            Assert.False(File.Exists("foo/test.txt"));
        }

        [SvnBridgeFact]
        public void RemoveFolderLocallyThenRemoveFolderInFolderAtServerWillNotRetreiveFolder()
        {
            CheckoutAndChangeDirectory();
            Directory.CreateDirectory("foo");
            File.WriteAllText("foo/test.txt", "hab");
            Svn("add foo");
            Svn("commit -m blah");
            Svn("update");

            RmDir("foo");
            Assert.False(File.Exists("foo/test.txt"));

            DeleteItem(testPath + "/foo", true);
            string svn = Svn("update");
            Assert.Equal("D    foo\r\nUpdated to revision " + _provider.GetLatestVersion() + ".", svn.Trim());
            Assert.False(Directory.Exists("foo"));
        }


        [SvnBridgeFact]
        public void AfterAnErrorWhenGettingFile_WillBeAbleToUpdateAgain()
        {
            CheckoutAndChangeDirectory();

            File.WriteAllText("foo.bar", "blah");

            Svn("add foo.bar");
            Svn("commit -m blah");

            WriteFile(testPath + "/test.txt", "as", true);
            File.WriteAllText("test.txt", "hab");
            SvnExpectError("update");
            File.Delete("test.txt");

            string svn = Svn("update");
            Assert.True(
                Regex.IsMatch(svn, @"^At revision \d+\.\r\n$")
                );
        }


        [SvnBridgeFact]
        public void AfterAnErrorWhenGettingFile_WillBeAbleToUpdateAgain_AndGetModifiedFile()
        {
            CheckoutAndChangeDirectory();

            File.WriteAllText("foo.bar", "blah");

            Svn("add foo.bar");
            Svn("commit -m blah");

            WriteFile(testPath + "/test.txt", "as", true);
            File.WriteAllText("test.txt", "hab");
            SvnExpectError("update");
            File.Delete("test.txt");

            WriteFile(testPath + "/foo.bar", "12312", true);

            Svn("update");

            Assert.Equal("12312", File.ReadAllText("foo.bar"));
        }


        [SvnBridgeFact]
        public void UpdatingFileWhenItIsMissingInWorkingCopy()
        {
            CheckoutAndChangeDirectory();

            File.WriteAllText("foo.bar", "12312");

            Svn("add foo.bar");
            Svn("commit -m blah");

            Svn("propset blah b .");
            Svn("commit -m blah");

            Svn("update foo.bar --revision PREV");

			Assert.False(File.Exists("foo.bar"));

            Svn("update");

            Assert.Equal("12312", File.ReadAllText("foo.bar"));
        }

        [SvnBridgeFact]
        public void UpdatingFolderWhenItIsMissingInWorkingCopy()
        {
            CheckoutAndChangeDirectory();

            Directory.CreateDirectory("foo");

            Svn("add foo");
            Svn("commit -m blah");

            Svn("propset blah b .");
            Svn("commit -m blah");

            Svn("update foo --revision PREV");

            Assert.False(Directory.Exists("foo"));

            Svn("update");

            Assert.True(Directory.Exists("foo"));
        }


        [SvnBridgeFact]
        public void CanGetLatestChangesWhenMovingBackward()
        {
            CheckoutAndChangeDirectory();

            // v 1.0
            File.WriteAllText("test.txt", "hab");
            Svn("add test.txt");
            Svn("commit -m blah");

            // v 2.0
            File.WriteAllText("test2.txt", "hab");
            Svn("add test2.txt");
            Svn("commit -m blah");

            // v 3.0
            File.WriteAllText("test.txt", "hab123");
            Svn("commit -m blah2");

            int previousVersion = _provider.GetLatestVersion() - 1;

            Svn("update");

            Svn("update test.txt --revision " + previousVersion);

            Assert.Equal("hab", File.ReadAllText("test.txt"));
        }

        [SvnBridgeFact]
        public void WhenFileInFolderIsInPreviousVersionAndUpdatingToLatestShouldUpdateFile()
        {
            CheckoutAndChangeDirectory();

            CreateFolder(testPath + "/TestFolder1", true);
            WriteFile(testPath + "/TestFolder1/blah.txt", "abc", true);
            UpdateFile(testPath + "/TestFolder1/blah.txt", "def", true);

            Svn("update");
            Svn("update TestFolder1/blah.txt --revision PREV");
            Assert.Equal("abc", File.ReadAllText("TestFolder1/blah.txt"));
            Svn("update");
            Assert.Equal("def", File.ReadAllText("TestFolder1/blah.txt"));
        }


        [SvnBridgeFact]
        public void UpdateAfterEditAndRenameOperation()
        {
            CheckoutAndChangeDirectory();

            CreateFolder(testPath + "/TestFolder1", true);
            WriteFile(testPath + "/TestFolder1/blah.txt", "abc", true);
            RenameItem(testPath + "/TestFolder1/blah.txt", testPath + "/TestFolder1/blah2.txt", false);
            UpdateFile(testPath + "/TestFolder1/blah2.txt", "bcd", true);

            Svn("update");

            Assert.Equal("bcd", File.ReadAllText("TestFolder1/blah2.txt"));
        }


        [SvnBridgeFact]
        public void UpdateAfterEditThenBackOneVersion()
        {
            CheckoutAndChangeDirectory();

            CreateFolder(testPath + "/TestFolder1", true);
            WriteFile(testPath + "/TestFolder1/blah.txt", "abc", true);
            Svn("update");
            WriteFile(testPath + "/test.txt", "abc", true);
            CreateFolder(testPath + "/TestFolder2", true);
            Svn("update TestFolder1 -r PREV");
        }

        [SvnBridgeFact]
        public void UpdateAfterEditAndMovePathOperation()
        {
            CheckoutAndChangeDirectory();

            CreateFolder(testPath + "/TestFolder1", true);
            WriteFile(testPath + "/TestFolder1/blah.txt", "abc", true);
            Svn("update");
            WriteFile(testPath + "/test.txt", "abc", true);
            CreateFolder(testPath + "/TestFolder2", true);
            Svn("update");

            RenameItem(testPath + "/TestFolder1/blah.txt", testPath + "/TestFolder2/blah.txt", true);

            Svn("propset test file .");

            // the root directory will now be at the head revision, but the other 
            // directories are not updated, so we have different versions
            Svn("commit -m \"force different versions in directories\" ");

            WriteFile(testPath + "/TestFolder1/blah.txt", "143", true);
            UpdateFile(testPath + "/TestFolder2/blah.txt", "bcd", true);


            Svn("update");

            Assert.Equal("bcd", File.ReadAllText("TestFolder2/blah.txt"));
            Assert.Equal("143", File.ReadAllText("TestFolder1/blah.txt"));
        }

        [SvnBridgeFact]
        public void UpdateFileInSubSubDirectoryThenUpdateRepositoryWillUpdateAllRevisions()
        {
            CheckoutAndChangeDirectory();

            CreateFolder(testPath + "/trunk", true);
            WriteFile(testPath + "/test.txt", "blah", true);
            CreateFolder(testPath + "/trunk/b", true);
            WriteFile(testPath + "/trunk/test.txt", "blah", true);
            WriteFile(testPath + "/trunk/b/asdf.txt", "blah", true);

            Svn("update");

            File.WriteAllText("trunk/b/asdf.txt", "adsa");

            Svn("commit trunk/b/asdf.txt -m test");
            Svn("update");
            XmlDocument xml = SvnXml("info --xml -R");
            int version = _provider.GetLatestVersion();
            foreach (XmlNode node in xml.SelectNodes("/info/entry/@revision"))
            {
                Assert.Equal(version, int.Parse(node.Value));
            }
        }

        [SvnBridgeFact]
        public void UpdateAfterCommitShouldNotGetAnything()
        {
            CheckoutAndChangeDirectory();
            File.WriteAllText("test.txt", @"1
2
3
4
5");
            Svn("add test.txt");

            Svn("commit -m test");

            string svn = Svn("update");
            int version = _provider.GetLatestVersion();
            Assert.Equal("At revision " + version + ".", svn.Trim());
        }


        [SvnBridgeFact]
        public void UpdateAfterCommitShouldGetChangesFromRepositoryToAnotherFile()
        {
            CheckoutAndChangeDirectory();
            File.WriteAllText("test.txt", @"1
2
3
4
5");
            Svn("add test.txt");

            Svn("commit -m test");

            WriteFile(testPath + "/test2.txt", "12345", true);

            string svn = Svn("update");
            int version = _provider.GetLatestVersion();
            Assert.Equal("A    test2.txt\r\nUpdated to revision " + version + ".", svn.Trim());
        }

        [SvnBridgeFact]
        public void UpdateAfterDelete()
        {
            CheckoutAndChangeDirectory();
            File.WriteAllText("test.txt", @"1");
            Svn("add test.txt");
            Svn("commit -m test");

            Svn("del test.txt");

            Svn("commit -m delete");

            Svn("update");
        }


        [SvnBridgeFact]
        public void UpdateAfterRemovingFolderFromFileSystemShouldReturnFolder()
        {
            CreateFolder(testPath + "/testFolder1", true);
            CheckoutAndChangeDirectory();

            RmDir("testFolder1");

            Svn("update");
            Assert.True(Directory.Exists("testFolder1"));
        }

        private static void RmDir(string directory)
        {
            ForAllFilesIn(directory, delegate(FileInfo info)
            {
                info.Attributes = info.Attributes & ~FileAttributes.ReadOnly;
            });
            Directory.Delete(directory, true);
        }

        [SvnBridgeFact]
        public void UpdateAfterRemovingFolderFromFileSystemShouldReturnFolderAndAllItsFiles()
        {
            CreateFolder(testPath + "/testFolder1", true);
            WriteFile(testPath + "/testFolder1/blah.txt", "aas", true);
            CheckoutAndChangeDirectory();
            Assert.Equal("aas", File.ReadAllText("testFolder1/blah.txt"));

            ForAllFilesIn("testFolder1", delegate(FileInfo info)
            {
                info.Attributes = info.Attributes & ~FileAttributes.ReadOnly;
            });
            Directory.Delete("testFolder1", true);

            Svn("update");
            Assert.Equal("aas", File.ReadAllText("testFolder1/blah.txt"));
        }


        [SvnBridgeFact]
        public void UpdateAfterRemovingFileFromFileSystemShouldReturnFile()
        {
            WriteFile(testPath + "/test.txt", "abc", true);
            CheckoutAndChangeDirectory();
            File.Delete("test.txt");
            Svn("update");
            Assert.Equal("abc", File.ReadAllText("test.txt"));
        }


        [SvnBridgeFact]
        public void UpdateAfterRename()
        {
            WriteFile(testPath + "/test.txt", "abc", true);
            CreateFolder(testPath + "/TestFolder1", true);
            CheckoutAndChangeDirectory();
            Svn("rename test.txt TestFolder1/test.txt");
            Svn("commit -m move");
            Svn("update");
        }


        [SvnBridgeFact]
        public void UpdateUsingMixRevisionRepositoryWhenEntryIsDeletedInFuture()
        {
            WriteFile(testPath + "/test.txt", "abc", true);
            CheckoutAndChangeDirectory();
            Svn("del test.txt");
            int version = _provider.GetLatestVersion();
            Svn("commit -m del");
            Svn("up -r " + version);

            Svn("propset b a .");
            Svn("commit -m update_root_to_new_rev");

            Svn("update");
        }

        [SvnBridgeFact]
        public void AfterAbortedCheckoutShouldUpdateFiles()
        {
            WriteFile(testPath + "/test.txt", "abc", true);
            WriteFile(testPath + "/test2.txt", "abc", true);

            string dir = Path.Combine(Environment.CurrentDirectory, testPath.Substring(1));
            Console.WriteLine("md " + dir);
            Directory.CreateDirectory(dir);

            string test1File = Path.Combine(dir, "test.txt");
            string test2File = Path.Combine(dir, "test2.txt");

            Console.WriteLine("echo sfg  " + test1File);
            File.WriteAllText(test1File, "sfg");

            SvnExpectError("co " + testUrl);

            Console.WriteLine("del " + test1File);
            File.Delete(test1File);

            Svn("update \"" + dir + "\"");

            Assert.Equal("abc", File.ReadAllText(test1File));
            Assert.Equal("abc", File.ReadAllText(test2File));
        }

        [SvnBridgeFact]
        public void UpdateAfterCommitShouldMergeChangesFromRepository()
        {
            CheckoutAndChangeDirectory();
            File.WriteAllText("test.txt", @"1
2
3
4
5");
            Svn("add test.txt");

            Svn("commit -m test");

            UpdateFile(testPath + "/test.txt", @"0
1
2
3
4
5", true);
            File.WriteAllText("test.txt", @"1
2
3
4
5
6");
            Svn("update");

            Assert.Equal(@"0
1
2
3
4
5
6", File.ReadAllText("test.txt"));
        }

        [SvnBridgeFact]
        public void UpdateAfterCommitWithModifiedThenDeletedFile_ShouldRemoveFile()
        {
            CheckoutAndChangeDirectory();
            File.WriteAllText("test.txt", "test");
            Svn("add test.txt");
            Svn("commit -m blah");
            WriteFile(testPath + "/test.txt", "test2", true);
			DeleteItem(testPath + "/test.txt", true);

            string output = Svn("up");

            Assert.False(File.Exists("test.txt"));
        }
    }
}
