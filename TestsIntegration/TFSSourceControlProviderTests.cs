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
        [IntegrationTestFact]
        public void TestAddFolderThatAlreadyExistsThrowsException()
		{
			CreateFolder(MergePaths(testPath, "/New Folder"), true);

            Exception result = Record.Exception(delegate { _provider.MakeCollection(_activityId, MergePaths(testPath, "/New Folder")); });

            Assert.IsType<FolderAlreadyExistsException>(result);
		}

		[IntegrationTestFact]
		public void TestDeleteItemReturnsFalseIfFileDoesNotExist()
		{
			bool result = _provider.DeleteItem(_activityId, MergePaths(testPath, "/NotHere.txt"));

			Assert.False(result);
		}

        [IntegrationTestFact]
        public void TestDeleteItemReturnsTrueWhenFileExists()
		{
			WriteFile(MergePaths(testPath, "/File.txt"), "filedata", true);

			bool result = _provider.DeleteItem(_activityId, MergePaths(testPath, "/File.txt"));

			Assert.True(result);
		}

        [IntegrationTestFact]
        public void TestItemExistsReturnsFalseIfFileDoesNotExist()
		{
			bool result = _provider.ItemExists(MergePaths(testPath, "/TestFile.txt"));

			Assert.False(result);
		}

        [IntegrationTestFact]
        public void TestItemExistsReturnsFalseIfFileDoesNotExistInSpecifiedVersion()
		{
			int version = _lastCommitRevision;
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);

			bool result = _provider.ItemExists(MergePaths(testPath, "/TestFile.txt"), version);

			Assert.False(result);
		}

		[IntegrationTestFact]
		public void TestItemExistsReturnsTrueIfFileExists()
		{
			WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);

			bool result = _provider.ItemExists(MergePaths(testPath, "/TestFile.txt"));

			Assert.True(result);
		}

        [IntegrationTestFact]
        public void GetVersionForDate_CurrentDateAndTime_ReturnsLatestChangeSet()
        {
            Thread.Sleep(1000);
            int result = _provider.GetVersionForDate(DateTime.Now);

            Assert.Equal(_lastCommitRevision, result);
        }

        [IntegrationTestFact]
        public void GetVersionForDate_DateAndTimeBeforeRepositoryExisted_ReturnsZero()
        {
            int result = _provider.GetVersionForDate(DateTime.Parse("1900-01-01"));

            Assert.Equal(0, result);
        }

        [IntegrationTestFact]
        public void GetVersionForDate_DateAndTimeBeforeLastCommit_ReturnsChangesetBeforeLastCommit()
        {
            Thread.Sleep(1000);
            int expected = _lastCommitRevision;
            DateTime startDate = DateTime.Now;
            Thread.Sleep(1000);
            WriteFile(MergePaths(testPath, "/TestFile.txt"), "Fun text", true);

            int result = _provider.GetVersionForDate(startDate);

            Assert.Equal(expected, result);
        }
    }
}
