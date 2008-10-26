namespace IntegrationTests
{
	using System.Collections.Generic;
	using SvnBridge.Protocol;
	using SvnBridge.SourceControl;
	using Xunit;

	public class TFSSourceControlProviderGetChangedItemsTests : TFSSourceControlProviderTestsBase
	{
		[Fact]
		public void TestGetChangedItemsAtRootReturnsNothingWhenClientStateAlreadyCurrent()
		{
			string path = MergePaths(testPath, "/TestFile.txt");
			int versionFrom = _lastCommitRevision;
			WriteFile(path, "Fun text", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();
			reportData.Entries = new List<EntryData>();
			EntryData entry = new EntryData();
			entry.Rev = versionTo.ToString();
			entry.path = MergePaths(testPath, "/TestFile.txt").Substring(1);
			reportData.Entries.Add(entry);

			FolderMetaData folder = _provider.GetChangedItems("/", versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsCompletesWhenChangesetDoesNotExistInPath()
		{
			int versionFrom = _lastCommitRevision;
			CreateFolder(MergePaths(testPath, "2"), true);
			CreateFolder(MergePaths(testPath, "/Folder1"), true);
			DeleteItem(MergePaths(testPath, "2"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsDoesNotIncludePropertiesInSubFoldersIfNotUpdated()
		{
			CreateFolder(MergePaths(testPath, "/Folder1"), false);
			SetProperty(testPath, "prop1", "val1", false);
			SetProperty(MergePaths(testPath, "/Folder1"), "prop2", "val2", true);
			int versionFrom = _lastCommitRevision;
			WriteFile(MergePaths(testPath, "/Folder1/Test1.txt"), "filedata", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
			Assert.Equal(1, ((FolderMetaData) folder.Items[0]).Items.Count);
			Assert.Equal(0, folder.Properties.Count);
			Assert.Equal(0, folder.Items[0].Properties.Count);
		}

		[Fact]
		public void TestGetChangedItemsForFileThatWasDeleted()
		{
			CreateFolder(MergePaths(testPath, "/New Folder"), false);
			WriteFile(MergePaths(testPath, "/New Folder/New File.txt"), "Fun text", true);
			int versionFrom = _lastCommitRevision;
			DeleteItem(MergePaths(testPath, "/New Folder"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();
			reportData.UpdateTarget = "New File.txt";

			FolderMetaData folder =
				_provider.GetChangedItems(MergePaths(testPath, "/New Folder"), versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
			Assert.Equal("New File.txt", folder.Items[0].Name);
			Assert.Equal(typeof (DeleteMetaData), folder.Items[0].GetType());
		}

		[Fact]
		public void TestGetChangedItemsWithAddedFile()
		{
			int versionFrom = _lastCommitRevision;
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(testPath, folder.Name);
			Assert.Equal(MergePaths(testPath, "/TestFile.txt"), folder.Items[0].Name);
			Assert.NotNull(folder.Items[0].DownloadUrl);
		}

		[Fact]
		public void TestGetChangedItemsWithAddedFileAtRoot()
		{
			int versionFrom = _lastCommitRevision;
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();
			CreateRootProvider();

			FolderMetaData folder = _providerRoot.GetChangedItems("", versionFrom, versionTo, reportData);

			Assert.Equal("/", folder.Name);
			Assert.Equal("/TestFile.txt", folder.Items[0].Name);
			Assert.NotNull(folder.Items[0].DownloadUrl);
		}
        
		[Fact]
		public void TestGetChangedItemsWithAddedFileContainingProperty()
		{
			int versionFrom = _lastCommitRevision;
			WriteFile(MergePaths(testPath, "/Test1.txt"), "filedata", false);
			SetProperty(MergePaths(testPath, "/Test1.txt"), "prop1", "prop1value", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
			Assert.Equal(1, folder.Items[0].Properties.Count);
			Assert.Equal(MergePaths(testPath, "/Test1.txt"), folder.Items[0].Name);
			Assert.Equal("prop1value", folder.Items[0].Properties["prop1"]);
		}

		[Fact]
		public void TestGetChangedItemsWithAddedFileReturnsNothingWhenClientStateAlreadyCurrent()
		{
			string path = MergePaths(testPath, "/TestFile.txt");
			WriteFile(path, "Fun text", true);
			int versionFrom = _lastCommitRevision;
			UpdateFile(path, "Fun text 2", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();
			reportData.Entries = new List<EntryData>();
			EntryData entry = new EntryData();
			entry.Rev = versionTo.ToString();
			entry.path = "TestFile.txt";
			reportData.Entries.Add(entry);

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithAddedFileThenDeletedFileReturnsNothing()
		{
			int versionFrom = _lastCommitRevision;
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);
			DeleteItem(MergePaths(testPath, "/TestFile.txt"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

        [Fact]
        public void TestGetChangedItemsWithUpdatedThenDeletedFile()
        {
            string path = MergePaths(testPath, "/TestFile.txt");
            WriteFile(path, "Fun text", true);
            int versionFrom = _lastCommitRevision;
            WriteFile(MergePaths(testPath, "/TestFile.txt"), "New fun text", true);
            DeleteItem(path, true);
            int versionTo = _lastCommitRevision;
            UpdateReportData reportData = new UpdateReportData();

            FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

            Assert.Equal(1, folder.Items.Count);
            Assert.True(folder.Items[0] is DeleteMetaData);
            Assert.Equal(path.Substring(1), folder.Items[0].Name);
        }

        [Fact]
		public void TestGetChangedItemsWithAddedFileThenEditedThenDeletedFileReturnsNothing()
		{
			int versionFrom = _lastCommitRevision;
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text2", true);
			DeleteItem(MergePaths(testPath, "/TestFile.txt"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithAddedFileThenFolderContainingFileIsDeleted()
		{
			CreateFolder(MergePaths(testPath, "/Folder1"), true);
			int versionFrom = _lastCommitRevision;
			WriteFile(MergePaths(testPath, "/Folder1/Test.txt"), "fun text", true);
			DeleteItem(MergePaths(testPath, "/Folder1"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
			Assert.Equal(MergePaths(testPath, "/Folder1"), folder.Items[0].Name);
			Assert.IsType(typeof (DeleteFolderMetaData), folder.Items[0]);
		}

		[Fact]
		public void TestGetChangedItemsWithAddedFolderAndFileWithinFolderInSingleCommitThenDeleteFolder()
		{
			int versionFrom = _lastCommitRevision;
			CreateFolder(MergePaths(testPath, "/Folder1"), false);
			WriteFile(MergePaths(testPath, "/Folder1/Test.txt"), "fun text", true);
			DeleteItem(MergePaths(testPath, "/Folder1"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithAddedFolderContainingProperty()
		{
			int versionFrom = _lastCommitRevision;
			CreateFolder(MergePaths(testPath, "/Folder1"), false);
			SetProperty(MergePaths(testPath, "/Folder1"), "prop1", "prop1value", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
			Assert.Equal(1, folder.Items[0].Properties.Count);
			Assert.Equal(MergePaths(testPath, "/Folder1"), folder.Items[0].Name);
			Assert.Equal("prop1value", folder.Items[0].Properties["prop1"]);
		}

		[Fact]
		public void TestGetChangedItemsWithAddedFolderPropertyThenDeletedFolder()
		{
			CreateFolder(MergePaths(testPath, "/Folder1"), true);
			int versionFrom = _lastCommitRevision;
			SetProperty(MergePaths(testPath, "/Folder1"), "prop", "val1", true);
			DeleteItem(MergePaths(testPath, "/Folder1"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithAddedFolderThenAddedPropertyThenDeletedFolder()
		{
			int versionFrom = _lastCommitRevision;
			CreateFolder(MergePaths(testPath, "/Folder1"), true);
			SetProperty(MergePaths(testPath, "/Folder1"), "prop", "val2", true);
			DeleteItem(MergePaths(testPath, "/Folder1"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithAddedFolderThenDeletedFolderReturnsNothing()
		{
			int versionFrom = _lastCommitRevision;
			CreateFolder(MergePaths(testPath, "/TestFolder"), true);
			DeleteItem(MergePaths(testPath, "/TestFolder"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithBranchedFile()
		{
			WriteFile(MergePaths(testPath, "/Fun.txt"), "Fun text", true);
			int versionFrom = _lastCommitRevision;
			CopyItem(MergePaths(testPath, "/Fun.txt"), MergePaths(testPath, "/FunRename.txt"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(testPath, folder.Name);
			Assert.Equal(1, folder.Items.Count);
			Assert.Equal(MergePaths(testPath, "/FunRename.txt"), folder.Items[0].Name);
			Assert.NotNull(folder.Items[0].DownloadUrl);
		}

		[Fact]
		public void TestGetChangedItemsWithDeletedAndReAddedItemReturnsNothingWhenClientStateAlreadyCurrent()
		{
			CreateFolder(MergePaths(testPath, "/New Folder"), true);
			int versionFrom = _lastCommitRevision;
			DeleteItem(MergePaths(testPath, "/New Folder"), true);
			CreateFolder(MergePaths(testPath, "/New Folder"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();
			EntryData entry = new EntryData();
			reportData.Entries = new List<EntryData>();
			entry.Rev = versionFrom.ToString();
			reportData.Entries.Add(entry);
			entry = new EntryData();
			entry.path = "New Folder";
			entry.Rev = versionTo.ToString();
			reportData.Entries.Add(entry);

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithDeletedFile()
		{
			string path = MergePaths(testPath, "/TestFile.txt");
			WriteFile(path, "Test file contents", true);
			int versionFrom = _lastCommitRevision;
			DeleteItem(path, true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.True(folder.Items[0] is DeleteMetaData);
			Assert.Equal(path.Substring(1), folder.Items[0].Name);
		}

		[Fact]
		public void TestGetChangedItemsWithDeletedFileReturnsNothingWhenClientStateAlreadyCurrent()
		{
			string path = MergePaths(testPath, "/TestFile.txt");
			WriteFile(path, "Fun text", true);
			int versionFrom = _lastCommitRevision;
			DeleteItem(path, true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();
			reportData.Missing = new List<string>();
			reportData.Missing.Add("TestFile.txt");

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithDeletedFileThenDeleteFolderContainingFile()
		{
			CreateFolder(MergePaths(testPath, "/Folder1"), false);
			WriteFile(MergePaths(testPath, "/Folder1/Test.txt"), "fun text", true);
			int versionFrom = _lastCommitRevision;
			DeleteItem(MergePaths(testPath, "/Folder1/Test.txt"), true);
			DeleteItem(MergePaths(testPath, "/Folder1"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
			Assert.Equal(MergePaths(testPath, "/Folder1"), folder.Items[0].Name);
			Assert.IsType(typeof (DeleteFolderMetaData), folder.Items[0]);
		}

		[Fact]
		public void TestGetChangedItemsWithDeletedFolder()
		{
			string path = MergePaths(testPath, "/Test Folder");
			CreateFolder(path, true);
			int versionFrom = _lastCommitRevision;
			DeleteItem(path, true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.True(folder.Items[0] is DeleteFolderMetaData);
			Assert.Equal(path.Substring(1), folder.Items[0].Name);
		}

		[Fact]
		public void TestGetChangedItemsWithDeletedFolderContainingFilesReturnsNothingWhenClientStateAlreadyCurrent()
		{
			CreateFolder(MergePaths(testPath, "/FolderA"), false);
			WriteFile(MergePaths(testPath, "/FolderA/Test1.txt"), "filedata", true);
			int versionFrom = _lastCommitRevision;
			DeleteItem(MergePaths(testPath, "/FolderA"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();
			reportData.Entries = new List<EntryData>();
			EntryData entry = new EntryData();
			entry.Rev = versionFrom.ToString();
			reportData.Entries.Add(entry);
			reportData.Missing = new List<string>();
			reportData.Missing.Add("FolderA");

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithDeletedFolderReturnsNothingWhenClientStateAlreadyCurrent()
		{
			string path = MergePaths(testPath, "/FolderA");
			CreateFolder(path, true);
			int versionFrom = _lastCommitRevision;
			DeleteItem(path, true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();
			reportData.Entries = new List<EntryData>();
			EntryData entry = new EntryData();
			entry.Rev = versionFrom.ToString();
			reportData.Entries.Add(entry);
			reportData.Missing = new List<string>();
			reportData.Missing.Add("FolderA");

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithDeleteFileThenDeleteFolderThatContainedFileWithinSubfolder()
		{
			CreateFolder(MergePaths(testPath, "/Test1"), false);
			CreateFolder(MergePaths(testPath, "/Test1/Folder1"), false);
			WriteFile(MergePaths(testPath, "/Test1/Folder1/Test.txt"), "fun text", true);
			int versionFrom = _lastCommitRevision;
			DeleteItem(MergePaths(testPath, "/Test1/Folder1/Test.txt"), true);
			DeleteItem(MergePaths(testPath, "/Test1/Folder1"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
			Assert.Equal(MergePaths(testPath, "/Test1"), folder.Items[0].Name);
			Assert.Equal(1, ((FolderMetaData) folder.Items[0]).Items.Count);
			Assert.Equal(MergePaths(testPath, "/Test1/Folder1"), ((FolderMetaData) folder.Items[0]).Items[0].Name);
			Assert.True(((FolderMetaData) folder.Items[0]).Items[0] is DeleteFolderMetaData);
			Assert.Equal(0, ((FolderMetaData) ((FolderMetaData) folder.Items[0]).Items[0]).Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithNewFileInNewFolderInSameChangeset()
		{
			int versionFrom = _lastCommitRevision;
			CreateFolder(MergePaths(testPath, "/New Folder"), false);
			WriteFile(MergePaths(testPath, "/New Folder/New File.txt"), "Fun text", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
			Assert.Equal(MergePaths(testPath, "/New Folder"), folder.Items[0].Name);
			Assert.Equal(1, ((FolderMetaData) folder.Items[0]).Items.Count);
			Assert.Equal(MergePaths(testPath, "/New Folder/New File.txt"),
			             ((FolderMetaData) folder.Items[0]).Items[0].Name);
		}

		[Fact]
		public void TestGetChangedItemsWithNewFolderAndNewFileReturnsNothingWhenClientStateAlreadyCurrent()
		{
			int versionFrom = _lastCommitRevision;
			CreateFolder(MergePaths(testPath, "/Folder1"), false);
			WriteFile(MergePaths(testPath, "/Folder1/Test.txt"), "filedata", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();
			reportData.Entries = new List<EntryData>();
			reportData.Entries.Add(new EntryData());
			reportData.Entries[0].Rev = versionFrom.ToString();
			reportData.Entries.Add(new EntryData());
			reportData.Entries[1].Rev = versionTo.ToString();
			reportData.Entries[1].path = "Folder1";

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithNoUpdatesDoesNotIncludeProperties()
		{
			SetProperty(testPath, "prop1", "val1", true);
			int versionFrom = _lastCommitRevision;
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
			Assert.Equal(testPath, folder.Name);
			Assert.Equal(versionTo, folder.Revision);
			Assert.Equal(0, folder.Properties.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithRenamedFile()
		{
			WriteFile(MergePaths(testPath, "/Fun.txt"), "Fun text", true);
			int versionFrom = _lastCommitRevision;
			MoveItem(MergePaths(testPath, "/Fun.txt"), MergePaths(testPath, "/FunRename.txt"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(testPath, folder.Name);
			Assert.Equal(2, folder.Items.Count);
			Assert.Equal(MergePaths(testPath, "/Fun.txt"), folder.Items[0].Name);
			Assert.True(folder.Items[0] is DeleteMetaData);
			Assert.Equal(MergePaths(testPath, "/FunRename.txt"), folder.Items[1].Name);
			Assert.NotNull(folder.Items[1]);
		}

		[Fact]
		public void TestGetChangedItemsWithRenamedFileReturnsNothingWhenClientStateAlreadyCurrent()
		{
			CreateFolder(MergePaths(testPath, "/Folder"), false);
			WriteFile(MergePaths(testPath, "/Fun.txt"), "Fun text", true);
			int versionFrom = _lastCommitRevision;
			MoveItem(MergePaths(testPath, "/Fun.txt"), MergePaths(testPath, "/FunRenamed.txt"), true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();
			reportData.Missing = new List<string>();
			reportData.Missing.Add("Fun.txt");
			reportData.Entries = new List<EntryData>();
			EntryData entry = new EntryData();
			entry.Rev = versionTo.ToString();
			entry.path = "FunRenamed.txt";
			reportData.Entries.Add(entry);

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
		}

		[Fact]
		public void TestGetChangedItemsWithSameFileUpdatedTwice()
		{
			string path = MergePaths(testPath, "/TestFile.txt");
			WriteFile(path, "Fun text", true);
			int versionFrom = _lastCommitRevision;
			UpdateFile(path, "Fun text 2", true);
			UpdateFile(path, "Fun text 3", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
			Assert.Equal(path, folder.Items[0].Name);
			Assert.Equal("Fun text 3", ReadFile(path));
		}

		[Fact]
		public void TestGetChangedItemsWithUpdatedFileProperty()
		{
			WriteFile(MergePaths(testPath, "/Test1.txt"), "filedata", true);
			int versionFrom = _lastCommitRevision;
			SetProperty(MergePaths(testPath, "/Test1.txt"), "prop1", "prop1value", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
			Assert.Equal(1, folder.Items[0].Properties.Count);
			Assert.Equal(MergePaths(testPath, "/Test1.txt"), folder.Items[0].Name);
			Assert.Equal("prop1value", folder.Items[0].Properties["prop1"]);
		}

		[Fact]
		public void TestGetChangedItemsWithUpdatedFolderProperty()
		{
			CreateFolder(MergePaths(testPath, "/Folder1"), true);
			int versionFrom = _lastCommitRevision;
			SetProperty(MergePaths(testPath, "/Folder1"), "prop1", "prop1value", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(1, folder.Items.Count);
			Assert.Equal(1, folder.Items[0].Properties.Count);
			Assert.Equal(MergePaths(testPath, "/Folder1"), folder.Items[0].Name);
			Assert.Equal("prop1value", folder.Items[0].Properties["prop1"]);
		}

		[Fact]
		public void TestGetChangedItemsWithUpdatedPropertyAtRoot()
		{
			int versionFrom = _lastCommitRevision;
			SetProperty(testPath, "prop1", "val1", true);
			int versionTo = _lastCommitRevision;
			UpdateReportData reportData = new UpdateReportData();

			FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

			Assert.Equal(0, folder.Items.Count);
			Assert.Equal(testPath, folder.Name);
			Assert.Equal("val1", folder.Properties["prop1"]);
		}

        [Fact]
        public void GetChangedItems_ClientStateHasUpdatedFileAndFileIsUpdated()
        {
            int versionFrom = _lastCommitRevision;
            WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);
            int versionUpdate = _lastCommitRevision;
            WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text 2", true);
            int versionTo = _lastCommitRevision;

            UpdateReportData reportData = new UpdateReportData();
            reportData.Entries = new List<EntryData>();
            EntryData entry = new EntryData();
            entry.Rev = versionFrom.ToString();
            reportData.Entries.Add(entry);
            entry = new EntryData();
            entry.Rev = versionUpdate.ToString();
            entry.path = "TestFile.txt";
            reportData.Entries.Add(entry);

            FolderMetaData folder = _provider.GetChangedItems(testPath, versionFrom, versionTo, reportData);

            Assert.Equal(1, folder.Items.Count);
            Assert.Equal(testPath, folder.Name);
            Assert.Equal(MergePaths(testPath, "/TestFile.txt"), folder.Items[0].Name);
            Assert.Equal(versionTo, folder.Items[0].ItemRevision);
        }
    }
}