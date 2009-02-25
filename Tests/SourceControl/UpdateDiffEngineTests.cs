using System;
using System.Collections.Generic;
using System.Text;
using SvnBridge.SourceControl;
using Xunit;
using CodePlex.TfsLibrary.ObjectModel;
using Tests;
using Attach;
using ChangeType = CodePlex.TfsLibrary.RepositoryWebSvc.ChangeType;
using ItemType = CodePlex.TfsLibrary.RepositoryWebSvc.ItemType;
using System.Diagnostics;

namespace UnitTests
{
    public class UpdateDiffEngineTests
    {
        MyMocks stub = new MyMocks();
        FolderMetaData root = new FolderMetaData("project");
        string checkoutRootPath = "";
        int targetVersion = 1;
        TFSSourceControlProvider sourceControlProvider = null;
        Dictionary<string, int> clientExistingFiles = new Dictionary<string, int>();
        Dictionary<string, string> clientMissingFiles = new Dictionary<string, string>();
        List<string> renamedItemsToBeCheckedForDeletedChildren = new List<string>();
        Dictionary<ItemMetaData, bool> additionForPropertyChangeOnly = new Dictionary<ItemMetaData, bool>();

        UpdateDiffEngine engine;

        public UpdateDiffEngineTests()
        {
            sourceControlProvider = stub.CreateTFSSourceControlProviderStub();
            engine = new UpdateDiffEngine(root, checkoutRootPath, targetVersion, sourceControlProvider, clientExistingFiles, clientMissingFiles, additionForPropertyChangeOnly, renamedItemsToBeCheckedForDeletedChildren);
        }

        [Fact]
        public void AddFile()
        {
            ItemMetaData item = new ItemMetaData("project/file.txt");
            stub.Attach(sourceControlProvider.GetItems, Return.Value(item));

            engine.Add(CreateChange(ChangeType.Add, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 1);
            AssertItem(root.Items[0], "project/file.txt", 0);
        }

        [Fact]
        public void AddFolder()
        {
            FolderMetaData item = new FolderMetaData("project/new folder");
            stub.Attach(sourceControlProvider.GetItems, Return.Value(item));

            engine.Add(CreateChange(ChangeType.Add, "project/new folder", ItemType.Folder));

            AssertFolder(root, "project", 0, 1);
            AssertFolder(root.Items[0], "project/new folder", 0, 0);
        }

        [Fact]
        public void AddFolderThenAddFileWithinFolder()
        {
            FolderMetaData folder = new FolderMetaData("project/new folder");
            ItemMetaData item = new ItemMetaData("project/new folder/file.txt");
            stub.Attach(sourceControlProvider.GetItems, Return.MultipleValues(folder, item));

            engine.Add(CreateChange(ChangeType.Add, "project/new folder", ItemType.Folder));
            engine.Add(CreateChange(ChangeType.Add, "project/new folder/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 1);
            AssertFolder(root.Items[0], "project/new folder", 0, 1);
            AssertItem(((FolderMetaData)root.Items[0]).Items[0], "project/new folder/file.txt", 0);
        }

        [Fact]
        public void AddFileThenEditFile()
        {
            ItemMetaData item = CreateItem("project/file.txt", 1);
            stub.Attach(sourceControlProvider.GetItems, Return.Value(item));

            engine.Add(CreateChange(ChangeType.Add, "project/file.txt", ItemType.File));
            engine.Edit(CreateChange(ChangeType.Edit, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 1);
            AssertItem(root.Items[0], "project/file.txt", 1);
        }

        [Fact]
        public void AddFileThenDeleteFile()
        {
            ItemMetaData item = new ItemMetaData("project/file.txt");
            stub.Attach(sourceControlProvider.GetItems, Return.Value(item));

            engine.Add(CreateChange(ChangeType.Add, "project/file.txt", ItemType.File));
            engine.Delete(CreateChange(ChangeType.Delete, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 0);
        }

        [Fact]
        public void AddFileThenEditFileThenDeleteFile()
        {
            ItemMetaData item = new ItemMetaData("project/file.txt");
            stub.Attach(sourceControlProvider.GetItems, Return.Value(item));

            engine.Add(CreateChange(ChangeType.Add, "project/file.txt", ItemType.File));
            engine.Edit(CreateChange(ChangeType.Edit, "project/file.txt", ItemType.File));
            engine.Delete(CreateChange(ChangeType.Delete, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 0);
        }

        [Fact]
        public void DeleteFile()
        {
            engine.Delete(CreateChange(ChangeType.Delete, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 1);
            AssertDeleteItem(root.Items[0], "project/file.txt");
        }

        [Fact]
        public void DeleteFileThenAddFile_ReturnsUpdatedFile()
        {
            ItemMetaData item = CreateItem("project/file.txt", 1);
            stub.Attach(sourceControlProvider.GetItems, Return.Value(item));

            engine.Delete(CreateChange(ChangeType.Delete, "project/file.txt", ItemType.File));
            engine.Add(CreateChange(ChangeType.Add, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 1);
            AssertItem(root.Items[0], "project/file.txt", 1);
        }

        [Fact(Skip="Temporary disable")]
        public void DeleteFileThenAddFileThenDeleteFile()
        {
            stub.Attach(sourceControlProvider.GetItems, Return.Value(null));

            engine.Delete(CreateChange(ChangeType.Delete, "project/file.txt", ItemType.File));
            engine.Add(CreateChange(ChangeType.Add, "project/file.txt", ItemType.File));
            engine.Delete(CreateChange(ChangeType.Delete, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 1);
            AssertDeleteItem(root.Items[0], "project/file.txt");
        }

        [Fact]
        public void DeleteFolder()
        {
            engine.Delete(CreateChange(ChangeType.Delete, "project/new folder", ItemType.Folder));

            AssertFolder(root, "project", 0, 1);
            AssertDeleteFolder(root.Items[0], "project/new folder", 0);
        }

        [Fact]
        public void DeleteFolderContainingTwoFiles()
        {
            engine.Delete(CreateChange(ChangeType.Delete, "project/new folder", ItemType.Folder));
            engine.Delete(CreateChange(ChangeType.Delete, "project/new folder/test1.txt", ItemType.File));
            engine.Delete(CreateChange(ChangeType.Delete, "project/new folder/test2.txt", ItemType.File));

            AssertFolder(root, "project", 0, 1);
            AssertDeleteFolder(root.Items[0], "project/new folder", 0);
        }

        [Fact]
        public void EditFile()
        {
            ItemMetaData item = new ItemMetaData("project/file.txt");
            stub.Attach(sourceControlProvider.GetItems, Return.Value(item));

            engine.Edit(CreateChange(ChangeType.Edit, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 1);
            AssertItem(root.Items[0], "project/file.txt", 0);
        }

        [Fact]
        public void EditFileThenDeleteFile()
        {
            stub.Attach(sourceControlProvider.GetItems, Return.Value(null));

            engine.Edit(CreateChange(ChangeType.Edit, "project/file.txt", ItemType.File));
            engine.Delete(CreateChange(ChangeType.Delete, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 1);
            AssertDeleteItem(root.Items[0], "project/file.txt");
        }

        [Fact]
        public void AddFileWhenClientStateCurrent()
        {
            ItemMetaData item = CreateItem("project/file.txt", 1);
            stub.Attach(sourceControlProvider.GetItems, Return.Value(item));
            clientExistingFiles.Add("/project/file.txt", 1);

            engine.Add(CreateChange(ChangeType.Add, "project/file.txt", 1, ItemType.File));

            AssertFolder(root, "project", 0, 0);
        }

        [Fact]
        public void AddFileWhenClientStateCurrentThenEditFile()
        {
            ItemMetaData item = CreateItem("project/file.txt", 2);
            stub.Attach(sourceControlProvider.GetItems, Return.Value(item));
            clientExistingFiles.Add("/project/file.txt", 1);

            engine.Add(CreateChange(ChangeType.Add, "project/file.txt", 1, ItemType.File));
            engine.Edit(CreateChange(ChangeType.Edit, "project/file.txt", 2, ItemType.File));

            AssertFolder(root, "project", 0, 1);
            AssertItem(root.Items[0], "project/file.txt", 2);
        }

        [Fact]
        public void DeleteFileWhenClientStateCurrent()
        {
            clientMissingFiles.Add("/project/file.txt", "file.txt");

            engine.Delete(CreateChange(ChangeType.Delete, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 0);
        }

        [Fact]
        public void AddFileThenDeleteFileWhenClientStateCurrent()
        {
            ItemMetaData item = CreateItem("project/file.txt", 1);
            stub.Attach(sourceControlProvider.GetItems, Return.Value(null));
            clientMissingFiles.Add("/project/file.txt", "file.txt");

            engine.Add(CreateChange(ChangeType.Add, "project/file.txt", ItemType.File));
            engine.Delete(CreateChange(ChangeType.Delete, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 0);
        }

        [Fact]
        public void DeleteFileThenUnDeleteFile()
        {
            ItemMetaData item = CreateItem("project/file.txt", 1);
            stub.Attach(sourceControlProvider.GetItems, Return.Value(item));

            engine.Delete(CreateChange(ChangeType.Delete, "project/file.txt", ItemType.File));
            engine.Add(CreateChange(ChangeType.Undelete | ChangeType.Edit, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 1);
            AssertItem(root.Items[0], "project/file.txt", 1);
        }

        private void AssertFolder(object folder, string name, int changeset, int itemCount)
        {
            Assert.IsType<FolderMetaData>(folder);
            Assert.Equal(name, ((FolderMetaData)folder).Name);
            Assert.Equal(ItemType.Folder, ((FolderMetaData)folder).ItemType);
            Assert.Equal(changeset, ((FolderMetaData)folder).Revision);
            Assert.Equal(itemCount, ((FolderMetaData)folder).Items.Count);
        }

        private void AssertItem(object item, string name, int changeset)
        {
            Assert.IsType<ItemMetaData>(item);
            Assert.Equal(name, ((ItemMetaData)item).Name);
            Assert.Equal(ItemType.File, ((ItemMetaData)item).ItemType);
            Assert.Equal(changeset, ((ItemMetaData)item).Revision);
        }

        private void AssertDeleteItem(object item, string name)
        {
            Assert.IsType<DeleteMetaData>(item);
            Assert.Equal(name, ((DeleteMetaData)item).Name);
            Assert.Equal(ItemType.File, ((DeleteMetaData)item).ItemType);
        }

        private void AssertDeleteFolder(object item, string name, int changeset)
        {
            Assert.IsType<DeleteFolderMetaData>(item);
            Assert.Equal(name, ((DeleteFolderMetaData)item).Name);
            Assert.Equal(ItemType.Folder, ((DeleteFolderMetaData)item).ItemType);
            Assert.Equal(changeset, ((DeleteFolderMetaData)item).Revision);
        }

        [DebuggerStepThrough]
        private SourceItemChange CreateChange(ChangeType changeType, string remoteName, ItemType itemType)
        {
            return CreateChange(changeType, remoteName, 0, itemType);
        }

        [DebuggerStepThrough]
        private SourceItemChange CreateChange(ChangeType changeType, string remoteName, int changeset, ItemType itemType)
        {
            SourceItem sourceItem = new SourceItem();
            sourceItem.RemoteName = remoteName;
            sourceItem.RemoteChangesetId = changeset;
            sourceItem.ItemType = itemType;
            SourceItemChange change = new SourceItemChange(sourceItem, changeType);
            return change;
        }

        private ItemMetaData CreateItem(string name, int changeset)
        {
            ItemMetaData item = new ItemMetaData(name);
            item.ItemRevision = changeset;
            return item;
        }
    }
}
