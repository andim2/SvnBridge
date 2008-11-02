using System;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Net;
using SvnBridge.SourceControl;
using Xunit;

namespace IntegrationTests
{
	public class TFSSourceControlProviderCommitTests : TFSSourceControlProviderTestsBase
	{
		[Fact]
		public void TestConcurrentCommits()
		{
			string activity1 = Guid.NewGuid().ToString();
			string activity2 = Guid.NewGuid().ToString();
			_provider.MakeActivity(activity1);
			_provider.MakeActivity(activity2);

			_provider.WriteFile(activity1, MergePaths(testPath, "/Fun.txt"), new byte[] { 1, 2, 3, 4 });
			_provider.WriteFile(activity2, MergePaths(testPath, "/Fun2.txt"), new byte[] { 1, 2, 3, 4 });

			_provider.MergeActivity(activity2);

			_provider.MergeActivity(activity1);
			RequestCache.Init();

			FolderMetaData items = (FolderMetaData)_provider.GetItems(_provider.GetLatestVersion(), testPath, Recursion.Full);
			Assert.Equal(2, items.Items.Count);
			Assert.Equal(MergePaths(testPath, "/Fun.txt"), items.Items[0].Name);
			Assert.Equal(MergePaths(testPath, "/Fun2.txt"), items.Items[1].Name);
		}

		[Fact]
		public void TestCommitBranchFile()
		{
			WriteFile(MergePaths(testPath, "/Fun.txt"), "Fun text", true);

			_provider.CopyItem(_activityId, MergePaths(testPath, "/Fun.txt"), MergePaths(testPath, "/FunBranch.txt"));
			MergeActivityResponse response = Commit();

			LogItem log =
				_provider.GetLog(MergePaths(testPath, "/FunBranch.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Branch, log.History[0].Changes[0].ChangeType & ChangeType.Branch);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(0, response.Items.Count);
		}

		[Fact]
		public void TestCommitBranchFolder()
		{
			CreateFolder(MergePaths(testPath, "/Fun"), true);

			_provider.CopyItem(_activityId, MergePaths(testPath, "/Fun"), MergePaths(testPath, "/FunBranch"));
			MergeActivityResponse response = Commit();

			LogItem log = _provider.GetLog(MergePaths(testPath, "/FunBranch"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Branch, log.History[0].Changes[0].ChangeType & ChangeType.Branch);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(0, response.Items.Count);
		}

        [Fact]
        public void TestCommitBranchFolderAlsoBranchesSubFiles()
        {
            CreateFolder(MergePaths(testPath, "/Folder1"), false);
            WriteFile(MergePaths(testPath, "/Folder1/Fun.txt"), "Fun text", true);

            _provider.CopyItem(_activityId, MergePaths(testPath, "/Folder1"), MergePaths(testPath, "/Folder2"));
            MergeActivityResponse response = Commit();

            FolderMetaData folder = (FolderMetaData)_provider.GetItems(-1, MergePaths(testPath, "/Folder2"), Recursion.Full);
            Assert.Equal(1, folder.Items.Count);
        }

		[Fact]
		public void TestCommitDeleteFile()
		{
			string path = MergePaths(testPath, "/TestFile.txt");
			WriteFile(path, "Test file contents", true);

			_provider.DeleteItem(_activityId, path);
			MergeActivityResponse response = Commit();

			Assert.False(_provider.ItemExists(path));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(1, response.Items.Count);
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitDeleteFileAlsoDeletesPropertiesOnFile()
		{
			string mimeType = "application/octet-stream";
			string path = MergePaths(testPath, "/TestFile.txt");
			WriteFile(path, "Fun text", false);
			SetProperty(path, "mime-type", mimeType, true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/TestFile.txt"));
			MergeActivityResponse response = Commit();

			ItemMetaData item = _provider.GetItems(-1, testPath, Recursion.Full);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(1, response.Items.Count);
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitDeleteFolder()
		{
			string path = MergePaths(testPath, "/TestFolder");
			CreateFolder(path, true);

			_provider.DeleteItem(_activityId, path);
			MergeActivityResponse response = Commit();

			Assert.False(_provider.ItemExists(path));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(1, response.Items.Count);
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitMoveAnUpdatedFileThenDeleteFolderThatContainedFile()
		{
			CreateFolder(MergePaths(testPath, "/A"), false);
			CreateFolder(MergePaths(testPath, "/B"), false);
			WriteFile(MergePaths(testPath, "/A/Test1.txt"), "filedata", true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/A"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A/Test1.txt"), MergePaths(testPath, "/B/Test1.txt"));
			_provider.WriteFile(_activityId, MergePaths(testPath, "/B/Test1.txt"), GetBytes("filedata2"));
			MergeActivityResponse response = Commit();

			LogItem log2 =
				_provider.GetLog(MergePaths(testPath, "/B/Test1.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename | ChangeType.Edit, log2.History[0].Changes[0].ChangeType);
			Assert.Equal("filedata2", ReadFile(MergePaths(testPath, "/B/Test1.txt")));
			Assert.False(_provider.ItemExists(MergePaths(testPath, "/A")));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(3, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B/Test1.txt"), ItemType.File));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B"), ItemType.Folder));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitMoveAnUpdateFile()
		{
			CreateFolder(MergePaths(testPath, "/Nodes"), false);
			WriteFile(MergePaths(testPath, "/Nodes/Fun.txt"), "filedata", false);
			CreateFolder(MergePaths(testPath, "/Protocol"), true);
			byte[] fileData = GetBytes("filedata2");

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/Nodes/Fun.txt"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/Nodes/Fun.txt"), MergePaths(testPath, "/Protocol/Fun.txt"));
			bool created = _provider.WriteFile(_activityId, MergePaths(testPath, "/Protocol/Fun.txt"), fileData);
			MergeActivityResponse response = Commit();

			LogItem log =
				_provider.GetLog(MergePaths(testPath, "/Protocol/Fun.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename | ChangeType.Edit, log.History[0].Changes[0].ChangeType);
			Assert.Equal(GetString(fileData), ReadFile(MergePaths(testPath, "/Protocol/Fun.txt")));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(3, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/Protocol/Fun.txt"), ItemType.File));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/Protocol"), ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/Nodes"), ItemType.Folder));
		}

		[Fact]
		public void TestCommitMoveFileFromDeletedFolder()
		{
			CreateFolder(MergePaths(testPath, "/A"), false);
			WriteFile(MergePaths(testPath, "/A/Test.txt"), "filedata", true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/A"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A/Test.txt"), MergePaths(testPath, "/Test.txt"));
			MergeActivityResponse response = Commit();

			LogItem log = _provider.GetLog(MergePaths(testPath, "/Test.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log.History[0].Changes[0].ChangeType);
			Assert.False(_provider.ItemExists(MergePaths(testPath, "/A")));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/Test.txt"), ItemType.File));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitMoveFileOutOfFolderAndDeleteFolder()
		{
			CreateFolder(MergePaths(testPath, "/TestFolder"), false);
			bool created = WriteFile(MergePaths(testPath, "/TestFolder/TestFile.txt"), "Test file contents", true);

			_provider.CopyItem(_activityId, MergePaths(testPath, "/TestFolder/TestFile.txt"), MergePaths(testPath, "/FunFile.txt"));
			_provider.DeleteItem(_activityId, MergePaths(testPath, "/TestFolder"));
			MergeActivityResponse response = Commit();

			LogItem log =
				_provider.GetLog(MergePaths(testPath, "/FunFile.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log.History[0].Changes[0].ChangeType);
			Assert.Null(_provider.GetItems(-1, MergePaths(testPath, "/TestFolder"), Recursion.None));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/FunFile.txt"), ItemType.File));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitMoveFolderWithUpdatedFile()
		{
			CreateFolder(MergePaths(testPath, "/A"), false);
			WriteFile(MergePaths(testPath, "/A/Test.txt"), "filedata", false);
			CreateFolder(MergePaths(testPath, "/B"), true);
			byte[] fileData = GetBytes("filedata2");

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/A"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A"), MergePaths(testPath, "/B/A"));
			bool created = _provider.WriteFile(_activityId, MergePaths(testPath, "/B/A/Test.txt"), fileData);
			MergeActivityResponse response = Commit();

			LogItem log =
				_provider.GetLog(MergePaths(testPath, "/B/A/Test.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename | ChangeType.Edit, log.History[0].Changes[0].ChangeType);
			Assert.Equal(GetString(fileData), ReadFile(MergePaths(testPath, "/B/A/Test.txt")));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(4, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B/A/Test.txt"), ItemType.File));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B/A"), ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B"), ItemType.Folder));
		}

		[Fact]
		public void TestCommitMoveMultipleFilesFromDeletedFolder()
		{
			CreateFolder(MergePaths(testPath, "/A"), false);
			WriteFile(MergePaths(testPath, "/A/Test1.txt"), "filedata", false);
			WriteFile(MergePaths(testPath, "/A/Test2.txt"), "filedata", true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/A"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A/Test1.txt"), MergePaths(testPath, "/Test1.txt"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A/Test2.txt"), MergePaths(testPath, "/Test2.txt"));
			MergeActivityResponse response = Commit();

			LogItem log = _provider.GetLog(MergePaths(testPath, "/Test1.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log.History[0].Changes[0].ChangeType);
			log = _provider.GetLog(MergePaths(testPath, "/Test2.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log.History[0].Changes[0].ChangeType);
			Assert.False(_provider.ItemExists(MergePaths(testPath, "/A")));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(3, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/Test1.txt"), ItemType.File));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/Test2.txt"), ItemType.File));
		}

		[Fact]
		public void TestCommitMoveMultipleFilesFromDeletedFolderIntoNewFolder()
		{
			CreateFolder(MergePaths(testPath, "/A"), false);
			WriteFile(MergePaths(testPath, "/A/Test1.txt"), "filedata", false);
			WriteFile(MergePaths(testPath, "/A/Test2.txt"), "filedata", true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/A"));
			_provider.MakeCollection(_activityId, MergePaths(testPath, "/B"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A/Test1.txt"), MergePaths(testPath, "/B/Test1.txt"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A/Test2.txt"), MergePaths(testPath, "/B/Test2.txt"));
			MergeActivityResponse response = Commit();

			LogItem log1 =
				_provider.GetLog(MergePaths(testPath, "/B/Test1.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log1.History[0].Changes[0].ChangeType);
			LogItem log2 =
				_provider.GetLog(MergePaths(testPath, "/B/Test2.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log2.History[0].Changes[0].ChangeType);
			Assert.False(_provider.ItemExists(MergePaths(testPath, "/A")));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(4, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B/Test1.txt"), ItemType.File));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B"), ItemType.Folder));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B/Test2.txt"), ItemType.File));
		}

		[Fact]
		public void TestCommitMultipleNewFiles()
		{
			byte[] testFile = GetBytes("Test file contents");

			_provider.WriteFile(_activityId, MergePaths(testPath, "/TestFile1.txt"), testFile);
			_provider.WriteFile(_activityId, MergePaths(testPath, "/TestFile2.txt"), testFile);
			_provider.WriteFile(_activityId, MergePaths(testPath, "/TestFile3.txt"), testFile);
			MergeActivityResponse response = Commit();

			Assert.Equal(GetString(testFile), ReadFile(MergePaths(testPath, "/TestFile1.txt")));
			Assert.Equal(GetString(testFile), ReadFile(MergePaths(testPath, "/TestFile2.txt")));
			Assert.Equal(GetString(testFile), ReadFile(MergePaths(testPath, "/TestFile3.txt")));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(4, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFile1.txt"), ItemType.File));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFile2.txt"), ItemType.File));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFile3.txt"), ItemType.File));
		}

		[Fact]
		public void TestCommitMultipleNewPropertiesOnMultipleFiles()
		{
			WriteFile(MergePaths(testPath, "/TestFile1.txt"), "Fun text", false);
			WriteFile(MergePaths(testPath, "/TestFile2.txt"), "Fun text", true);

			_provider.SetProperty(_activityId, MergePaths(testPath, "/TestFile1.txt"), "mime-type1", "mime1");
			_provider.SetProperty(_activityId, MergePaths(testPath, "/TestFile1.txt"), "mime-type2", "mime2");
			_provider.SetProperty(_activityId, MergePaths(testPath, "/TestFile2.txt"), "mime-type3", "mime3");
			_provider.SetProperty(_activityId, MergePaths(testPath, "/TestFile2.txt"), "mime-type4", "mime4");
			MergeActivityResponse response = Commit();

			FolderMetaData item = (FolderMetaData)_provider.GetItems(-1, testPath, Recursion.Full);
			Assert.Equal("mime1", item.Items[0].Properties["mime-type1"]);
			Assert.Equal("mime2", item.Items[0].Properties["mime-type2"]);
			Assert.Equal("mime3", item.Items[1].Properties["mime-type3"]);
			Assert.Equal("mime4", item.Items[1].Properties["mime-type4"]);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFile1.txt"), ItemType.File));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFile2.txt"), ItemType.File));
		}

		[Fact]
		public void TestCommitMultipleNewPropertiesOnMultipleFolders()
		{
			CreateFolder(MergePaths(testPath, "/Folder1"), false);
			CreateFolder(MergePaths(testPath, "/Folder2"), true);

			_provider.SetProperty(_activityId, MergePaths(testPath, "/Folder1"), "mime-type1", "mime1");
			_provider.SetProperty(_activityId, MergePaths(testPath, "/Folder1"), "mime-type2", "mime2");
			_provider.SetProperty(_activityId, MergePaths(testPath, "/Folder2"), "mime-type3", "mime3");
			_provider.SetProperty(_activityId, MergePaths(testPath, "/Folder2"), "mime-type4", "mime4");
			MergeActivityResponse response = Commit();

			FolderMetaData item = (FolderMetaData)_provider.GetItems(-1, testPath, Recursion.Full);
			Assert.Equal("mime1", item.Items[0].Properties["mime-type1"]);
			Assert.Equal("mime2", item.Items[0].Properties["mime-type2"]);
			Assert.Equal("mime3", item.Items[1].Properties["mime-type3"]);
			Assert.Equal("mime4", item.Items[1].Properties["mime-type4"]);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/Folder1"), ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/Folder2"), ItemType.Folder));
		}

		[Fact]
		public void TestCommitNewFile()
		{
			byte[] testFile = GetBytes("Test file contents");

			bool created = _provider.WriteFile(_activityId, MergePaths(testPath, "/TestFile.txt"), testFile);
			MergeActivityResponse response = Commit();

			Assert.Equal(GetString(testFile), ReadFile(MergePaths(testPath, "/TestFile.txt")));
			Assert.Equal(true, created);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFile.txt"), ItemType.File));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitNewFolder()
		{
			_provider.MakeCollection(_activityId, MergePaths(testPath, "/TestFolder"));
			MergeActivityResponse response = Commit();

			Assert.True(_provider.ItemExists(MergePaths(testPath, "/TestFolder")));
			Assert.Equal(ItemType.Folder, _provider.GetItems(-1, MergePaths(testPath, "/TestFolder"), Recursion.None).ItemType);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFolder"), ItemType.Folder));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitNewFolderContainingNewFile()
		{
			byte[] fileData = GetBytes("Test file contents");

			_provider.MakeCollection(_activityId, MergePaths(testPath, "/TestFolder"));
			_provider.WriteFile(_activityId, MergePaths(testPath, "/TestFolder/TestFile.txt"), fileData);
			MergeActivityResponse response = Commit();

			Assert.Equal(GetString(fileData), ReadFile(MergePaths(testPath, "/TestFolder/TestFile.txt")));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(3, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFolder/TestFile.txt"), ItemType.File));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFolder"), ItemType.Folder));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitNewPropertyOnFile()
		{
			string mimeType = "application/octet-stream";
			string path = MergePaths(testPath, "/TestFile.txt");
			WriteFile(path, "Fun text", true);

			_provider.SetProperty(_activityId, path, "mime-type", mimeType);
			MergeActivityResponse response = Commit();

			FolderMetaData item = (FolderMetaData)_provider.GetItems(-1, testPath, Recursion.Full);
			Assert.Equal(mimeType, item.Items[0].Properties["mime-type"]);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(1, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFile.txt"), ItemType.File));
		}

		[Fact]
		public void TestCommitNewPropertyOnFolder()
		{
			string ignore = "*.bad\n";

			_provider.SetProperty(_activityId, testPath, "svn:ignore", ignore);
			MergeActivityResponse response = Commit();

			FolderMetaData item = (FolderMetaData)_provider.GetItems(-1, testPath, Recursion.Full);
			Assert.Equal(ignore, item.Properties["svn:ignore"]);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(1, response.Items.Count);
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitNewPropertyOnNewFileInSameCommit()
		{
			byte[] fileData = GetBytes("test");

			_provider.WriteFile(_activityId, MergePaths(testPath, "/TestFile1.txt"), fileData);
			_provider.SetProperty(_activityId, MergePaths(testPath, "/TestFile1.txt"), "mime-type1", "mime1");
			MergeActivityResponse response = Commit();

			FolderMetaData item = (FolderMetaData)_provider.GetItems(-1, testPath, Recursion.Full);
			Assert.Equal("mime1", item.Items[0].Properties["mime-type1"]);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFile1.txt"), ItemType.File));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitNewPropertyOnNewFolderInSameCommit()
		{
			_provider.MakeCollection(_activityId, MergePaths(testPath, "/Folder1"));
			_provider.SetProperty(_activityId, MergePaths(testPath, "/Folder1"), "mime-type1", "mime1");
			MergeActivityResponse response = Commit();

			FolderMetaData item = (FolderMetaData)_provider.GetItems(-1, testPath, Recursion.Full);
			Assert.Equal("mime1", item.Items[0].Properties["mime-type1"]);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/Folder1"), ItemType.Folder));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitNewSubFolderInNewFolder()
		{
			_provider.MakeCollection(_activityId, MergePaths(testPath, "/TestFolder"));
			_provider.MakeCollection(_activityId, MergePaths(testPath, "/TestFolder/SubFolder"));
			MergeActivityResponse response = Commit();

			Assert.True(_provider.ItemExists(MergePaths(testPath, "/TestFolder/SubFolder")));
			Assert.Equal(ItemType.Folder,
						 _provider.GetItems(-1, MergePaths(testPath, "/TestFolder/SubFolder"), Recursion.None).ItemType);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(3, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFolder"), ItemType.Folder));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFolder/SubFolder"), ItemType.Folder));
		}

		[Fact]
		public void TestCommitRenameAndUpdateFile()
		{
			WriteFile(MergePaths(testPath, "/Fun.txt"), "Fun text", true);
			byte[] updatedText = GetBytes("Test file contents");

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/Fun.txt"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/Fun.txt"), MergePaths(testPath, "/FunRename.txt"));
			bool created = _provider.WriteFile(_activityId, MergePaths(testPath, "/FunRename.txt"), updatedText);
			MergeActivityResponse response = Commit();

			LogItem log =
				_provider.GetLog(MergePaths(testPath, "/FunRename.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename | ChangeType.Edit, log.History[0].Changes[0].ChangeType);
			Assert.Equal(GetString(updatedText), ReadFile(MergePaths(testPath, "/FunRename.txt")));
			Assert.Equal(false, created);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/FunRename.txt"), ItemType.File));
		}

		[Fact]
		public void TestCommitRenameFile()
		{
			WriteFile(MergePaths(testPath, "/Fun.txt"), "Fun text", true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/Fun.txt"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/Fun.txt"), MergePaths(testPath, "/FunRename.txt"));
			MergeActivityResponse response = Commit();

			LogItem log =
				_provider.GetLog(MergePaths(testPath, "/FunRename.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log.History[0].Changes[0].ChangeType);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/FunRename.txt"), ItemType.File));
		}

		[Fact]
		public void TestCommitRenameFileWithCopyBeforeDelete()
		{
			WriteFile(MergePaths(testPath, "/FunRename.txt"), "Fun text", true);

			_provider.CopyItem(_activityId, MergePaths(testPath, "/FunRename.txt"), MergePaths(testPath, "/Fun.txt"));
			_provider.DeleteItem(_activityId, MergePaths(testPath, "/FunRename.txt"));
			MergeActivityResponse response = Commit();

			LogItem log = _provider.GetLog(MergePaths(testPath, "/Fun.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log.History[0].Changes[0].ChangeType);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/Fun.txt"), ItemType.File));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitRenameFolder()
		{
			CreateFolder(MergePaths(testPath, "/Fun"), true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/Fun"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/Fun"), MergePaths(testPath, "/FunRename"));
			MergeActivityResponse response = Commit();

			LogItem log = _provider.GetLog(MergePaths(testPath, "/FunRename"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log.History[0].Changes[0].ChangeType);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/FunRename"), ItemType.Folder));
		}

		[Fact]
		public void TestCommitRenameFolderAndDeleteFileWithinFolder()
		{
			CreateFolder(MergePaths(testPath, "/A"), false);
			WriteFile(MergePaths(testPath, "/A/Test1.txt"), "filedata", true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/A"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A"), MergePaths(testPath, "/B"));
			_provider.DeleteItem(_activityId, MergePaths(testPath, "/B/Test1.txt"));
			MergeActivityResponse response = Commit();

			LogItem log1 = _provider.GetLog(MergePaths(testPath, "/B"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log1.History[0].Changes[0].ChangeType);
			Assert.False(_provider.ItemExists(MergePaths(testPath, "/B/Test1.txt")));
			Assert.False(_provider.ItemExists(MergePaths(testPath, "/A")));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B"), ItemType.Folder));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitRenameFolderContainingRenamedFile()
		{
			CreateFolder(MergePaths(testPath, "/A"), false);
			WriteFile(MergePaths(testPath, "/A/Test1.txt"), "filedata", true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/A"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A"), MergePaths(testPath, "/B"));
			_provider.DeleteItem(_activityId, MergePaths(testPath, "/B/Test1.txt"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A/Test1.txt"), MergePaths(testPath, "/B/Test2.txt"));
			MergeActivityResponse response = Commit();

			LogItem log1 = _provider.GetLog(MergePaths(testPath, "/B"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log1.History[0].Changes[0].ChangeType);
			LogItem log2 =
				_provider.GetLog(MergePaths(testPath, "/B/Test2.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log2.History[0].Changes[0].ChangeType);
			Assert.False(_provider.ItemExists(MergePaths(testPath, "/A")));
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(3, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B"), ItemType.Folder));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B/Test2.txt"), ItemType.File));
		}

		[Fact]
		public void TestCommitRenameFolderContainingRenamedFileAndNotRenamedFile()
		{
			CreateFolder(MergePaths(testPath, "/A"), false);
			WriteFile(MergePaths(testPath, "/A/TestA1.txt"), "filedata", false);
			WriteFile(MergePaths(testPath, "/A/TestB1.txt"), "filedata", true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/A"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A"), MergePaths(testPath, "/B"));
			_provider.DeleteItem(_activityId, MergePaths(testPath, "/B/TestA1.txt"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A/TestA1.txt"), MergePaths(testPath, "/B/TestA2.txt"));
			MergeActivityResponse response = Commit();

			// Assert state of TFS database
			Assert.False(_provider.ItemExists(MergePaths(testPath, "/A")));
			Assert.True(_provider.ItemExists(MergePaths(testPath, "/B")));
			Assert.True(_provider.ItemExists(MergePaths(testPath, "/B/TestA2.txt")));
			Assert.True(_provider.ItemExists(MergePaths(testPath, "/B/TestB1.txt")));
			// Assert TFS history
			LogItem log1 = _provider.GetLog(MergePaths(testPath, "/B"), 1, _provider.GetLatestVersion(), Recursion.Full, 1);
			Assert.Equal(ChangeType.Rename, log1.History[0].Changes[0].ChangeType);
			Assert.Equal(ChangeType.Rename, log1.History[0].Changes[1].ChangeType);
			Assert.Equal(ChangeType.Rename, log1.History[0].Changes[2].ChangeType);
			// Assert commit output
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(3, response.Items.Count);
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B"), ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B/TestA2.txt"), ItemType.File));
		}

		[Fact]
		public void TestCommitRenameFolderContainingUpdatedFile()
		{
			CreateFolder(MergePaths(testPath, "/A"), false);
			WriteFile(MergePaths(testPath, "/A/Test.txt"), "filedata", true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/A"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A"), MergePaths(testPath, "/B"));
			bool created = _provider.WriteFile(_activityId, MergePaths(testPath, "/B/Test.txt"), GetBytes("filedata2"));
			MergeActivityResponse response = Commit();

			// Assert state of TFS database
			Assert.False(_provider.ItemExists(MergePaths(testPath, "/A")));
			Assert.True(_provider.ItemExists(MergePaths(testPath, "/B")));
			Assert.True(_provider.ItemExists(MergePaths(testPath, "/B/Test.txt")));
			Assert.Equal("filedata2", ReadFile(MergePaths(testPath, "/B/Test.txt")));
			// Assert TFS history
			LogItem log1 = _provider.GetLog(MergePaths(testPath, "/B"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log1.History[0].Changes[0].ChangeType);
			LogItem log2 =
				_provider.GetLog(MergePaths(testPath, "/B/Test.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Edit | ChangeType.Rename, log2.History[0].Changes[0].ChangeType);
			// Assert commit output
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(3, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B"), ItemType.Folder));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B/Test.txt"), ItemType.File));
		}

		[Fact]
		public void TestCommitRenameFolderWithPropertiesAddedInCommitAfterFolderCreated()
		{
			CreateFolder(MergePaths(testPath, "/A"), true);
			SetProperty(MergePaths(testPath, "/A"), "prop1", "val1", true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/A"));
			_provider.CopyItem(_activityId, MergePaths(testPath, "/A"), MergePaths(testPath, "/B"));
			MergeActivityResponse response = Commit();

			// Assert state of TFS database
			Assert.False(_provider.ItemExists(MergePaths(testPath, "/A")));
			Assert.True(_provider.ItemExists(MergePaths(testPath, "/B")));
			Assert.Equal("val1", _provider.GetItems(-1, MergePaths(testPath, "/B"), Recursion.None).Properties["prop1"]);
			// Assert TFS history
			LogItem log1 = _provider.GetLog(MergePaths(testPath, "/B"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Rename, log1.History[0].Changes[0].ChangeType);
			// Assert commit output
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/B"), ItemType.Folder));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitReplacedFile()
		{
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Test file contents", true);

			_provider.DeleteItem(_activityId, MergePaths(testPath, "/TestFile.txt"));
			_provider.WriteFile(_activityId, MergePaths(testPath, "/TestFile.txt"), GetBytes("new"));
			MergeActivityResponse response = Commit();

			// Assert state of TFS database
			Assert.True(_provider.ItemExists(MergePaths(testPath, "/TestFile.txt")));
			Assert.Equal("new", ReadFile(MergePaths(testPath, "/TestFile.txt")));
			// Assert TFS history
			LogItem log1 =
				_provider.GetLog(MergePaths(testPath, "/TestFile.txt"), 1, _provider.GetLatestVersion(), Recursion.None, 1);
			Assert.Equal(ChangeType.Edit, log1.History[0].Changes[0].ChangeType);
			// Assert commit output
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(2, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFile.txt"), ItemType.File));
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitUpdatedFile()
		{
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Test file contents", true);
			byte[] testFile = GetBytes("Test file contents\r\nUpdated");

			bool created = _provider.WriteFile(_activityId, MergePaths(testPath, "/TestFile.txt"), testFile);
			MergeActivityResponse response = Commit();

			Assert.Equal(GetString(testFile), ReadFile(MergePaths(testPath, "/TestFile.txt")));
			Assert.Equal(false, created);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(1, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFile.txt"), ItemType.File));
		}

		[Fact(Skip = "Not implemented yet")]
		//[ExpectedException(typeof (ConflictException))]
		public void TestCommitUpdatedFileAtSameTimeAsAnotherUserThrowsConflictException()
		{
			string activity2 = Guid.NewGuid().ToString();
			WriteFile(MergePaths(testPath, "/Test.txt"), "data", true);

			//_provider.Checkout(_activityId);
			_provider.MakeActivity(activity2);
			//_provider.Checkout(activity2);
			_provider.WriteFile(activity2, MergePaths(testPath, "/Test.txt"), GetBytes("new1"));
			_provider.MergeActivity(activity2);
			_provider.DeleteActivity(activity2);
			_provider.WriteFile(_activityId, MergePaths(testPath, "/Test.txt"), GetBytes("new2"));
			_provider.MergeActivity(_activityId);
		}

		[Fact]
		public void TestCommitUpdatePropertyOnFile()
		{
			string mimeType1 = "application/octet-stream1";
			string mimeType2 = "application/octet-stream2";
			string path = MergePaths(testPath, "/TestFile.txt");
			WriteFile(path, "Fun text", false);
			SetProperty(path, "mime-type", mimeType1, true);

			_provider.SetProperty(_activityId, path, "mime-type", mimeType2);
			MergeActivityResponse response = Commit();

			FolderMetaData item = (FolderMetaData)_provider.GetItems(-1, testPath, Recursion.Full);
			Assert.Equal(mimeType2, item.Items[0].Properties["mime-type"]);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(1, response.Items.Count);
			Assert.True(ResponseContains(response, MergePaths(testPath, "/TestFile.txt"), ItemType.File));
		}

		[Fact]
		public void TestCommitUpdatePropertyOnFolder()
		{
			string ignore1 = "*.bad\n";
			string ignore2 = "*.good\n";
			SetProperty(testPath, "ignore", ignore1, true);

			_provider.SetProperty(_activityId, testPath, "ignore", ignore2);
			MergeActivityResponse response = Commit();

			FolderMetaData item = (FolderMetaData)_provider.GetItems(-1, testPath, Recursion.Full);
			Assert.Equal(ignore2, item.Properties["ignore"]);
			Assert.Equal(_provider.GetLatestVersion(), response.Version);
			Assert.Equal(1, response.Items.Count);
			Assert.True(ResponseContains(response, testPath, ItemType.Folder));
		}

		[Fact]
		public void TestCommitWithNoItemsReturnsLatestChangeset()
		{
			int startVersion = _provider.GetLatestVersion();

			MergeActivityResponse response = Commit();

			int endVersion = _provider.GetLatestVersion();
			Assert.Equal(startVersion, endVersion);
			Assert.Equal(endVersion, response.Version);
			Assert.Equal(0, response.Items.Count);
		}
	}
}