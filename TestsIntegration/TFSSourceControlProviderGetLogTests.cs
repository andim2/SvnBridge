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
		public void GetLog_()
		{
			int versionFrom = _lastCommitRevision;
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(testPath, versionFrom, versionTo, Recursion.Full, Int32.MaxValue);

			Assert.Equal(2, logItem.History.Length);
		}

		[IntegrationTestFact]
		public void GetLog_ReturnsOriginalNameAndRevisionForRenamedItems()
		{
			WriteFile(MergePaths(testPath, "/Fun.txt"), "Fun text", true);
			int versionFrom = _lastCommitRevision;
			MoveItem(MergePaths(testPath, "/Fun.txt"), MergePaths(testPath, "/FunRename.txt"), true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(MergePaths(testPath, "/FunRename.txt"), versionFrom, versionTo, Recursion.None, 1);

            Assert.Equal(MergePaths(testPath, "/Fun.txt").Substring(1), ((RenamedSourceItem)logItem.History[0].Changes[0].Item).OriginalRemoteName);
			Assert.Equal(versionFrom, ((RenamedSourceItem)logItem.History[0].Changes[0].Item).OriginalRevision);
            Assert.Equal(MergePaths(testPath, "/FunRename.txt").Substring(1), logItem.History[0].Changes[0].Item.RemoteName);
		}

		[IntegrationTestFact]
		public void GetLog_WithBranchedFileContainsOriginalNameAndRevision()
		{
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);
			int versionFrom = _lastCommitRevision;
			CopyItem(MergePaths(testPath, "/TestFile.txt"), MergePaths(testPath, "/TestFileBranch.txt"), true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(testPath, versionTo, versionTo, Recursion.Full, Int32.MaxValue);

			Assert.Equal(ChangeType.Branch, logItem.History[0].Changes[0].ChangeType & ChangeType.Branch);
			Assert.Equal(MergePaths(testPath, "/TestFile.txt").Substring(1), ((RenamedSourceItem)logItem.History[0].Changes[0].Item).OriginalRemoteName);
			Assert.Equal(versionFrom, ((RenamedSourceItem)logItem.History[0].Changes[0].Item).OriginalRevision);
		}

        [IntegrationTestFact]
        public void GetLog_WithTwoBranchedFiles_ContainsOriginalNameAndRevisionForBoth()
        {
            WriteFile(MergePaths(testPath, "/TestFile1.txt"), "Fun1", false);
            WriteFile(MergePaths(testPath, "/TestFile2.txt"), "Fun2", true);
            int versionFrom = _lastCommitRevision;
            CopyItem(MergePaths(testPath, "/TestFile1.txt"), MergePaths(testPath, "/TestFile1Branch.txt"), false);
            CopyItem(MergePaths(testPath, "/TestFile2.txt"), MergePaths(testPath, "/TestFile2Branch.txt"), true);
            int versionTo = _lastCommitRevision;

            LogItem logItem = _provider.GetLog(testPath, versionTo, versionTo, Recursion.Full, Int32.MaxValue);

            Assert.Equal(ChangeType.Branch, logItem.History[0].Changes[0].ChangeType & ChangeType.Branch);
            Assert.Equal(ChangeType.Branch, logItem.History[0].Changes[1].ChangeType & ChangeType.Branch);
            Assert.Equal(MergePaths(testPath, "/TestFile1.txt").Substring(1), ((RenamedSourceItem)logItem.History[0].Changes[0].Item).OriginalRemoteName);
            Assert.Equal(MergePaths(testPath, "/TestFile2.txt").Substring(1), ((RenamedSourceItem)logItem.History[0].Changes[1].Item).OriginalRemoteName);
            Assert.Equal(versionFrom, ((RenamedSourceItem)logItem.History[0].Changes[0].Item).OriginalRevision);
            Assert.Equal(versionFrom, ((RenamedSourceItem)logItem.History[0].Changes[1].Item).OriginalRevision);
        }

        [IntegrationTestFact]
        public void GetLog_WhenFileIsBranchedTwice()
        {
            WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun", true);
            int versionFrom = _lastCommitRevision;
            CopyItem(MergePaths(testPath, "/TestFile.txt"), MergePaths(testPath, "/TestBranch1.txt"), true);
            CopyItem(MergePaths(testPath, "/TestFile.txt"), MergePaths(testPath, "/TestBranch2.txt"), true);
            int versionTo = _lastCommitRevision;

            LogItem logItem = _provider.GetLog(testPath, versionFrom + 1, versionTo, Recursion.Full, Int32.MaxValue);

            Assert.Equal(ChangeType.Branch, logItem.History[1].Changes[0].ChangeType & ChangeType.Branch);
            Assert.Equal(ChangeType.Branch, logItem.History[0].Changes[0].ChangeType & ChangeType.Branch);
            Assert.Equal(MergePaths(testPath, "/TestFile.txt").Substring(1), ((RenamedSourceItem)logItem.History[1].Changes[0].Item).OriginalRemoteName);
            Assert.Equal(MergePaths(testPath, "/TestFile.txt").Substring(1), ((RenamedSourceItem)logItem.History[0].Changes[0].Item).OriginalRemoteName);
            Assert.Equal(MergePaths(testPath, "/TestBranch1.txt").Substring(1), logItem.History[1].Changes[0].Item.RemoteName);
            Assert.Equal(MergePaths(testPath, "/TestBranch2.txt").Substring(1), logItem.History[0].Changes[0].Item.RemoteName);
            Assert.Equal(versionFrom, ((RenamedSourceItem)logItem.History[1].Changes[0].Item).OriginalRevision);
            Assert.Equal(versionFrom + 1, ((RenamedSourceItem)logItem.History[0].Changes[0].Item).OriginalRevision);
        }

		[IntegrationTestFact]
		public void GetLog_WithBranchedFileContainsOriginalVersionAsRevisionImmediatelyBeforeBranch()
		{
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);
			WriteFile(MergePaths(testPath, "/TestFile2.txt"), "Fun text", true);
			int versionFrom = _lastCommitRevision;
			CopyItem(MergePaths(testPath, "/TestFile.txt"), MergePaths(testPath, "/TestFileBranch.txt"), true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(testPath, versionTo, versionTo, Recursion.Full, Int32.MaxValue);

			Assert.Equal(versionFrom, ((RenamedSourceItem)logItem.History[0].Changes[0].Item).OriginalRevision);
		}

		[IntegrationTestFact]
		public void GetLog_WithNewFolder()
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