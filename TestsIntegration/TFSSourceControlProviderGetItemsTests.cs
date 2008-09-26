using System;
using SvnBridge.SourceControl;
using Xunit;

namespace IntegrationTests
{
    public class TFSSourceControlProviderGetItemsTests : TFSSourceControlProviderTestsBase
    {
        [Fact]
        public void TestGetItemInActivityReturnsCorrectItemIfIsInRenamedFolder()
        {
            CreateFolder(testPath + "/A", false);
            WriteFile(testPath + "/A/Test.txt", "filedata", true);
            DeleteItem(testPath + "/A", false);
            CopyItem(testPath + "/A", testPath + "/B", false);

            ItemMetaData item = _provider.GetItemInActivity(_activityId, testPath + "/B/Test.txt");

            Assert.Equal(testPath + "/A/Test.txt", item.Name);
        }

        [Fact]
        public void TestGetItemsForRootSucceedsWithAllRecursionLevels()
        {
            _provider.GetItems(-1, "", Recursion.None);
            _provider.GetItems(-1, "", Recursion.OneLevel);
            _provider.GetItems(-1, "", Recursion.Full);
        }

        [Fact]
        public void TestGetItemsOnFile()
        {
            WriteFile(testPath + "/File1.txt", "filedata", true);

            ItemMetaData item = _provider.GetItems(-1, testPath + "/File1.txt", Recursion.None);

            Assert.NotNull(item);
            Assert.Equal(testPath  + "/File1.txt", item.Name);
        }

        [Fact]
        public void TestGetItemsOnFileReturnsPropertiesForFile()
        {
            WriteFile(testPath + "/File1.txt", "filedata", false);
            string propvalue = "prop1value";
            SetProperty(testPath + "/File1.txt", "prop1", propvalue, true);

            ItemMetaData item = _provider.GetItems(-1, testPath + "/File1.txt", Recursion.None);

            Assert.Equal(propvalue, item.Properties["prop1"]);
        }

        [Fact]
        public void TestGetItemsOnFolderReturnsPropertiesForFileWithinFolder()
        {
            string mimeType = "application/octet-stream";
            string path = testPath + "/TestFile.txt";
            WriteFile(path, "Fun text", false);
            SetProperty(path, "mime-type", mimeType, true);

            FolderMetaData item = (FolderMetaData) _provider.GetItems(-1, testPath, Recursion.Full);

            Assert.Equal(mimeType, item.Items[0].Properties["mime-type"]);
        }

        [Fact]
        public void TestGetItemsOnRootFolderReturnsPropertiesForFileWithinFolder()
        {
            string mimeType = "application/octet-stream";
            string path = testPath + "/TestFile.txt";
            WriteFile(path, "Fun text", false);
            SetProperty(path, "mime-type", mimeType, true);
            CreateRootProvider();

            FolderMetaData folder = (FolderMetaData)_providerRoot.GetItems(-1, "", Recursion.Full);

            Assert.Equal(mimeType, folder.Items[0].Properties["mime-type"]);
            Assert.Equal(1, folder.Items.Count);
        }

        [Fact]
        public void TestGetItemsOnFolderReturnsPropertiesForFolder()
        {
            string ignore = "*.bad\n";
            SetProperty(testPath, "ignore", ignore, true);

            FolderMetaData item = (FolderMetaData) _provider.GetItems(-1, testPath, Recursion.Full);

            Assert.Equal(ignore, item.Properties["ignore"]);
        }

        [Fact]
        public void TestGetItemsOnRootFolderReturnsPropertiesForFolder()
        {
            string ignore = "*.bad\n";
            SetProperty(testPath, "ignore", ignore, true);
            CreateRootProvider();

            FolderMetaData item = (FolderMetaData)_providerRoot.GetItems(-1, "", Recursion.Full);

            Assert.Equal(ignore, item.Properties["ignore"]);
            Assert.Equal(0, item.Items.Count);
        }

        [Fact]
        public void TestGetItemsReturnsCorrectRevisionWhenPropertyHasBeenAddedToFileAndRecursionIsFull()
        {
            WriteFile(testPath + "/Test.txt", "whee", true);
            SetProperty(testPath + "/Test.txt", "prop1", "val1", true);
            int revision = _lastCommitRevision;

            FolderMetaData item = (FolderMetaData) _provider.GetItems(-1, testPath, Recursion.Full);

            Assert.Equal(revision, item.Items[0].Revision);
        }

        [Fact]
        public void TestGetItemsReturnsCorrectRevisionWhenPropertyHasBeenAddedToFolderAndRecursionIsFull()
        {
            CreateFolder(testPath + "/Folder1", true);
            SetProperty(testPath + "/Folder1", "prop1", "val1", true);
            int revision = _lastCommitRevision;

            FolderMetaData item = (FolderMetaData) _provider.GetItems(-1, testPath, Recursion.Full);

            Assert.Equal(revision, item.Items[0].Revision);
        }

        [Fact]
        public void TestGetItemsReturnsCorrectRevisionWhenPropertyIsAdded()
        {
            WriteFile(testPath + "/File1.txt", "filedata", true);
            SetProperty(testPath + "/File1.txt", "prop1", "val1", true);
            int revision = _lastCommitRevision;

            ItemMetaData item = _provider.GetItems(-1, testPath + "/File1.txt", Recursion.None);

            Assert.Equal(revision, item.Revision);
        }

        [Fact]
        public void TestGetItemsReturnsCorrectRevisionWhenPropertyIsAddedThenFileIsUpdated()
        {
            WriteFile(testPath + "/File1.txt", "filedata", true);
            SetProperty(testPath + "/File1.txt", "prop1", "val1", true);
            WriteFile(testPath + "/File1.txt", "filedata2", true);
            int revision = _lastCommitRevision;

            ItemMetaData item = _provider.GetItems(-1, testPath + "/File1.txt", Recursion.None);

            Assert.Equal(revision, item.Revision);
        }

        [Fact]
        public void TestGetItemsWithAllRecursionLevelsReturnsPropertyForFolder()
        {
            CreateFolder(testPath + "/Folder1", false);
            string ignore = "*.bad\n";
            SetProperty(testPath + "/Folder1", "ignore", ignore, true);

            FolderMetaData item1 = (FolderMetaData) _provider.GetItems(-1, testPath + "/Folder1", Recursion.Full);
            FolderMetaData item2 = (FolderMetaData) _provider.GetItems(-1, testPath + "/Folder1", Recursion.OneLevel);
            FolderMetaData item3 = (FolderMetaData) _provider.GetItems(-1, testPath + "/Folder1", Recursion.None);

            Assert.Equal(ignore, item1.Properties["ignore"]);
            Assert.Equal(ignore, item2.Properties["ignore"]);
            Assert.Equal(ignore, item3.Properties["ignore"]);
        }

        [Fact]
        public void TestGetItemsWithOneLevelRecursionReturnsPropertiesForSubFolders()
        {
            CreateFolder(testPath + "/Folder1", false);
            CreateFolder(testPath + "/Folder1/SubFolder", false);
            string ignore1 = "*.bad1\n";
            string ignore2 = "*.bad2\n";
            SetProperty(testPath + "/Folder1", "ignore", ignore1, false);
            SetProperty(testPath + "/Folder1/SubFolder", "ignore", ignore2, true);

            FolderMetaData item = (FolderMetaData)_provider.GetItems(-1, testPath + "/Folder1", Recursion.OneLevel);

            Assert.Equal(ignore1, item.Properties["ignore"]);
            Assert.Equal(ignore2, item.Items[0].Properties["ignore"]);
        }

        [Fact]
        public void TestGetItemsIgnoresStalePropertyFiles()
        {
            string propertyFile = "<?xml version=\"1.0\" encoding=\"utf-8\"?><ItemProperties xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"><Properties><Property><Name>mime-type</Name><Value>application/octet-stream</Value></Property></Properties></ItemProperties>";
            CreateFolder(testPath + "/..svnbridge", true);
            WriteFile(testPath + "/..svnbridge/WheelMUD Database Creation.sql", GetBytes(propertyFile), true);

            Assert.DoesNotThrow(delegate
            {
                _provider.GetItems(-1, testPath, Recursion.Full);     
            });
        }
    }
}