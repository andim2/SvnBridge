using System;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.SourceControl;

namespace IntegrationTests
{
	using Xunit;

	public class TFSSourceControlProviderGetLogTests : TFSSourceControlProviderTestsBase
	{
		[IntegrationTestFact]
		public void TestGetLog()
		{
			int versionFrom = _lastCommitRevision;
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(testPath, versionFrom, versionTo, Recursion.Full, Int32.MaxValue);

			Assert.Equal(2, logItem.History.Length);
		}

		[IntegrationTestFact]
		public void TestGetLogReturnsOriginalNameAndRevisionForRenamedItems()
		{
			WriteFile(MergePaths(testPath, "/Fun.txt"), "Fun text", true);
			int versionFrom = _lastCommitRevision;
			MoveItem(MergePaths(testPath, "/Fun.txt"), MergePaths(testPath, "/FunRename.txt"), true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(MergePaths(testPath, "/FunRename.txt"), versionFrom, versionTo, Recursion.None, 1);

			Assert.Equal(MergePaths(testPath, "/Fun.txt"),
			                ((RenamedSourceItem) logItem.History[0].Changes[0].Item).OriginalRemoteName);
			Assert.Equal(versionFrom, ((RenamedSourceItem) logItem.History[0].Changes[0].Item).OriginalRevision);
            Assert.Equal(MergePaths(testPath, "/FunRename.txt").Substring(1), logItem.History[0].Changes[0].Item.RemoteName);
		}

		[IntegrationTestFact]
		public void TestGetLogWithBranchedFileContainsOriginalNameAndRevision()
		{
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);
			int versionFrom = _lastCommitRevision;
			CopyItem(MergePaths(testPath, "/TestFile.txt"), MergePaths(testPath, "/TestFileBranch.txt"), true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(testPath, versionTo, versionTo, Recursion.Full, Int32.MaxValue);

			Assert.Equal(ChangeType.Branch, logItem.History[0].Changes[0].ChangeType & ChangeType.Branch);
			Assert.Equal(MergePaths(testPath, "/TestFile.txt").Substring(1),
			                ((RenamedSourceItem) logItem.History[0].Changes[0].Item).OriginalRemoteName);
			Assert.Equal(versionFrom, ((RenamedSourceItem) logItem.History[0].Changes[0].Item).OriginalRevision);
		}

		[IntegrationTestFact]
		public void TestGetLogWithBranchedFileContainsOriginalVersionAsRevisionImmediatelyBeforeBranch()
		{
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);
			WriteFile(MergePaths(testPath, "/TestFile2.txt"), "Fun text", true);
			int versionFrom = _lastCommitRevision;
			CopyItem(MergePaths(testPath, "/TestFile.txt"), MergePaths(testPath, "/TestFileBranch.txt"), true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(testPath, versionTo, versionTo, Recursion.Full, Int32.MaxValue);

			Assert.Equal(versionFrom, ((RenamedSourceItem) logItem.History[0].Changes[0].Item).OriginalRevision);
		}

		[IntegrationTestFact]
		public void TestGetLogWithNewFolder()
		{
			int versionFrom = _lastCommitRevision;
			CreateFolder(MergePaths(testPath, "/TestFolder"), true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(testPath, versionFrom, versionTo, Recursion.Full, Int32.MaxValue);

			Assert.Equal(2, logItem.History.Length);
			Assert.Equal(1, logItem.History[0].Changes.Count);
			Assert.Equal(ChangeType.Add | ChangeType.Encoding, logItem.History[0].Changes[0].ChangeType);
			Assert.Equal(ItemType.Folder, logItem.History[0].Changes[0].Item.ItemType);
			Assert.Equal(MergePaths(testPath, "/TestFolder").Substring(1), logItem.History[0].Changes[0].Item.RemoteName);
		}

        [IntegrationTestFact]
        public void GetLog_Root_ReturnsCorrectResult()
        {
            LogItem logItem = _provider.GetLog("", 1, _lastCommitRevision, Recursion.None, Int32.MaxValue);

            Assert.Equal("", logItem.History[0].Changes[0].Item.RemoteName);
        }
    }
}