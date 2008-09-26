using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RegistrationWebSvc;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;
using SvnBridge.Cache;
using SvnBridge.Exceptions;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using SvnBridge.Infrastructure;
using IntegrationTests;
using Xunit;
using System;
using System.Threading;

namespace IntegrationTests
{
	public class TFSSourceControlProviderTests : TFSSourceControlProviderTestsBase
	{
		[Fact]
		public void TestAddFolderThatAlreadyExistsThrowsException()
		{
			CreateFolder(testPath + "/New Folder", true);

            Exception result = Record.Exception(delegate { _provider.MakeCollection(_activityId, testPath + "/New Folder"); });

            Assert.IsType<FolderAlreadyExistsException>(result);
		}

		[Fact]
		public void TestDeleteItemReturnsFalseIfFileDoesNotExist()
		{
			bool result = _provider.DeleteItem(_activityId, testPath + "/NotHere.txt");

			Assert.False(result);
		}

		[Fact]
		public void TestDeleteItemReturnsTrueWhenFileExists()
		{
			WriteFile(testPath + "/File.txt", "filedata", true);

			bool result = _provider.DeleteItem(_activityId, testPath + "/File.txt");

			Assert.True(result);
		}

		[Fact]
		public void TestItemExistsReturnsFalseIfFileDoesNotExist()
		{
			bool result = _provider.ItemExists(testPath + "/TestFile.txt");

			Assert.False(result);
		}

		[Fact]
		public void TestItemExistsReturnsFalseIfFileDoesNotExistInSpecifiedVersion()
		{
			int version = _lastCommitRevision;
			WriteFile(testPath + "/TestFile.txt", "Fun text", true);

			bool result = _provider.ItemExists(testPath + "/TestFile.txt", version);

			Assert.False(result);
		}

		[Fact]
		public void TestItemExistsReturnsTrueIfFileExists()
		{
			WriteFile(testPath + "/TestFile.txt", "Fun text", true);

			bool result = _provider.ItemExists(testPath + "/TestFile.txt");

			Assert.True(result);
		}

        [Fact]
        public void GetVersionForDate_CurrentDateAndTime_ReturnsLatestChangeSet()
        {
            int result = _provider.GetVersionForDate(DateTime.Now);

            Assert.Equal(_lastCommitRevision, result);
        }

        [Fact]
        public void GetVersionForDate_DateAndTimeBeforeRepositoryExisted_ReturnsZero()
        {
            int result = _provider.GetVersionForDate(DateTime.Parse("1900-01-01"));

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetVersionForDate_DateAndTimeBeforeProjectExistedButAfterRepositoryExisted_ReturnsZero()
        {
            LogItem item = _provider.GetLog("", 1, _lastCommitRevision, Recursion.None, int.MaxValue);
            DateTime checkTime = item.History[item.History.Length - 1].CommitDateTime.AddDays(-1);

            int result = _provider.GetVersionForDate(checkTime);

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetVersionForDate_DateAndTimeBeforeLastCommit_ReturnsChangesetBeforeLastCommit()
        {
            int expected = _lastCommitRevision;
            DateTime startDate = DateTime.Now;
            Thread.Sleep(1000);
            WriteFile(testPath + "/TestFile.txt", "Fun text", true);

            int result = _provider.GetVersionForDate(startDate);

            Assert.Equal(expected, result);
        }
    }
}
