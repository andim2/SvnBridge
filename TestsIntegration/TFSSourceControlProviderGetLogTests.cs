using System;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.SourceControl;

namespace IntegrationTests
{
	using Xunit;

	public class TFSSourceControlProviderGetLogTests : TFSSourceControlProviderTestsBase
	{
		[Fact]
		public void TestGetLog()
		{
			int versionFrom = _lastCommitRevision;
			WriteFile(testPath + "/TestFile.txt", "Fun text", true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(testPath, versionFrom, versionTo, Recursion.Full, Int32.MaxValue);

			Assert.Equal(2, logItem.History.Length);
		}

		[Fact]
		public void TestGetLogReturnsOriginalNameAndRevisionForRenamedItems()
		{
			WriteFile(testPath + "/Fun.txt", "Fun text", true);
			int versionFrom = _lastCommitRevision;
			MoveItem(testPath + "/Fun.txt", testPath + "/FunRename.txt", true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(testPath + "/FunRename.txt", versionFrom, versionTo, Recursion.None, 1);

			Assert.Equal(testPath + "/Fun.txt",
			                ((RenamedSourceItem) logItem.History[0].Changes[0].Item).OriginalRemoteName);
			Assert.Equal(versionFrom, ((RenamedSourceItem) logItem.History[0].Changes[0].Item).OriginalRevision);
			Assert.Equal(testPath.Substring(1) + "/FunRename.txt", logItem.History[0].Changes[0].Item.RemoteName);
		}

		[Fact]
		public void TestGetLogWithBranchedFileContainsOriginalNameAndRevision()
		{
			WriteFile(testPath + "/TestFile.txt", "Fun text", true);
			int versionFrom = _lastCommitRevision;
			CopyItem(testPath + "/TestFile.txt", testPath + "/TestFileBranch.txt", true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(testPath, versionTo, versionTo, Recursion.Full, Int32.MaxValue);

			Assert.Equal(ChangeType.Branch, logItem.History[0].Changes[0].ChangeType & ChangeType.Branch);
			Assert.Equal(testPath.Substring(1) + "/TestFile.txt",
			                ((RenamedSourceItem) logItem.History[0].Changes[0].Item).OriginalRemoteName);
			Assert.Equal(versionFrom, ((RenamedSourceItem) logItem.History[0].Changes[0].Item).OriginalRevision);
		}

		[Fact]
		public void TestGetLogWithBranchedFileContainsOriginalVersionAsRevisionImmediatelyBeforeBranch()
		{
			WriteFile(testPath + "/TestFile.txt", "Fun text", true);
			WriteFile(testPath + "/TestFile2.txt", "Fun text", true);
			int versionFrom = _lastCommitRevision;
			CopyItem(testPath + "/TestFile.txt", testPath + "/TestFileBranch.txt", true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(testPath, versionTo, versionTo, Recursion.Full, Int32.MaxValue);

			Assert.Equal(versionFrom, ((RenamedSourceItem) logItem.History[0].Changes[0].Item).OriginalRevision);
		}

		[Fact]
		public void TestGetLogWithNewFolder()
		{
			int versionFrom = _lastCommitRevision;
			CreateFolder(testPath + "/TestFolder", true);
			int versionTo = _lastCommitRevision;

			LogItem logItem = _provider.GetLog(testPath, versionFrom, versionTo, Recursion.Full, Int32.MaxValue);

			Assert.Equal(2, logItem.History.Length);
			Assert.Equal(1, logItem.History[0].Changes.Count);
			Assert.Equal(ChangeType.Add | ChangeType.Encoding, logItem.History[0].Changes[0].ChangeType);
			Assert.Equal(ItemType.Folder, logItem.History[0].Changes[0].Item.ItemType);
			Assert.Equal(testPath.Substring(1) + "/TestFolder", logItem.History[0].Changes[0].Item.RemoteName);
		}

        [Fact]
        public void GetLog_Root_ReturnsCorrectResult()
        {
            LogItem logItem = _provider.GetLog("", 1, _lastCommitRevision, Recursion.None, Int32.MaxValue);

            Assert.Equal("", logItem.History[0].Changes[0].Item.RemoteName);
        }
    }
}