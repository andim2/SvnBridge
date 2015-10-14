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
        ClientStateTracker clientStateTracker = new ClientStateTracker();
        List<string> renamedItemsToBeCheckedForDeletedChildren = new List<string>();
        Dictionary<ItemMetaData, bool> additionForPropertyChangeOnly = new Dictionary<ItemMetaData, bool>();

        UpdateDiffEngine engine;

        public UpdateDiffEngineTests()
        {
            sourceControlProvider = stub.CreateTFSSourceControlProviderStub();
            engine = new UpdateDiffEngine(
                root,
                checkoutRootPath,
                targetVersion,
                sourceControlProvider,
                clientStateTracker,
                additionForPropertyChangeOnly,
                renamedItemsToBeCheckedForDeletedChildren);
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

        [Fact]
        [Trait("TestName", "EFDTATDTATD")]
        public void ExistingFileDeleteThenAddThenDeleteThenAddThenDelete()
        {
            //System.Diagnostics.Debugger.Launch();

            string pathFile = "project/file.txt";

            ItemMetaData item = new ItemMetaData(pathFile);
            root.Items.Add(item);

            stub.Attach(sourceControlProvider.GetItems, Return.Value(root.Items.Count > 0 ? root.Items[0] : null));

            engine.Delete(CreateChange(ChangeType.Delete, pathFile, ItemType.File));
            engine.Add(CreateChange(ChangeType.Add, pathFile, ItemType.File));
            engine.Delete(CreateChange(ChangeType.Delete, pathFile, ItemType.File));
            engine.Add(CreateChange(ChangeType.Add, pathFile, ItemType.File));
            engine.Delete(CreateChange(ChangeType.Delete, pathFile, ItemType.File));

            AssertFolder(root, "project", 0, 1);
            var items = root.Items;
            // End result of a pre-existing file over this change range
            // *must* be registered as an active Delete
            // rather than simply a complementary revert of "prior item change".
            AssertDeleteItem(items[0], pathFile);
        }

        [Fact]
        [Trait("TestName", "FATDTATD")]
        public void FileAddThenDeleteThenAddThenDelete()
        {
            //System.Diagnostics.Debugger.Launch();

            string pathFile = "project/file.txt";

            stub.Attach(sourceControlProvider.GetItems, Return.Value(root.Items.Count > 0 ? root.Items[0] : null));

            engine.Add(CreateChange(ChangeType.Add, pathFile, ItemType.File));
            engine.Delete(CreateChange(ChangeType.Delete, pathFile, ItemType.File));
            engine.Add(CreateChange(ChangeType.Add, pathFile, ItemType.File));
            engine.Delete(CreateChange(ChangeType.Delete, pathFile, ItemType.File));

            // End result of a not-existing file over this change range
            // *must* be "no item change to be done".
            AssertFolder(root, "project", 0, 0);
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
        public void DeleteFolderThenAddFolderAgainContainingFile()
        {
            ItemMetaData item = new ItemMetaData("project/new folder/file.txt");
            FolderMetaData folder = new FolderMetaData("project/new folder");
            stub.Attach(sourceControlProvider.GetItems, Return.DelegateResult(delegate(object[] parameters) {
                if (((string)parameters[1]) == "project/new folder")
                    return folder;
                else if (((string)parameters[1]) == "project/new folder/file.txt")
                    return item;
                else
                    return null;
            }));

            engine.Delete(CreateChange(ChangeType.Delete, "project/new folder", ItemType.Folder));
            engine.Add(CreateChange(ChangeType.Add, "project/new folder/file.txt", ItemType.File));
            engine.Add(CreateChange(ChangeType.Add, "project/new folder", ItemType.Folder));

            AssertFolder(root, "project", 0, 1);
            AssertFolder(root.Items[0], "project/new folder", 0, 1);
            AssertItem(((FolderMetaData)root.Items[0]).Items[0], "project/new folder/file.txt", 0);
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
            clientStateTracker.SetFileExisting("/project/file.txt", 1);

            engine.Add(CreateChange(ChangeType.Add, "project/file.txt", 1, ItemType.File));

            AssertFolder(root, "project", 0, 0);
        }

        [Fact]
        public void AddFileWhenClientStateCurrentThenEditFile()
        {
            ItemMetaData item = CreateItem("project/file.txt", 2);
            stub.Attach(sourceControlProvider.GetItems, Return.Value(item));
            clientStateTracker.SetFileExisting("/project/file.txt", 1);

            engine.Add(CreateChange(ChangeType.Add, "project/file.txt", 1, ItemType.File));
            engine.Edit(CreateChange(ChangeType.Edit, "project/file.txt", 2, ItemType.File));

            AssertFolder(root, "project", 0, 1);
            AssertItem(root.Items[0], "project/file.txt", 2);
        }

        [Fact]
        public void DeleteFileWhenClientStateCurrent()
        {
            clientStateTracker.SetFileMissing("/project/file.txt", "file.txt");

            engine.Delete(CreateChange(ChangeType.Delete, "project/file.txt", ItemType.File));

            AssertFolder(root, "project", 0, 0);
        }

        [Fact]
        public void AddFileThenDeleteFileWhenClientStateCurrent()
        {
            ItemMetaData item = CreateItem("project/file.txt", 1);
            stub.Attach(sourceControlProvider.GetItems, Return.Value(null));
            clientStateTracker.SetFileMissing("/project/file.txt", "file.txt");

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

        [Fact]
        [Trait("TestName", "RFDRTCE")]
        public void RenameFileDoRochadeThenCheckExists()
        {
            //System.Diagnostics.Debugger.Launch();
            string pathBarOld = "project/BarOld.txt";
            string pathBar = "project/Bar.txt";
            int idItemBar = 20;
            string pathBarNew = "project/BarNew.txt";
            int idItemBarNew = 40;
            stub.Attach(sourceControlProvider.GetItems, Return.DelegateResult(delegate(object[] parameters)
            {
                int version = (int)parameters[0];
                string path = (string)parameters[1];
                //var recursion = parameters[2];

                switch (version)
                {
                    case 1:
                        if (pathBarOld == path)
                        {
                            ItemMetaData item = CreateItem(path, version);
                            item.Id = idItemBar;
                            return item;
                        }
                        else
                        if (pathBar == path)
                        {
                            ItemMetaData item = CreateItem(path, version);
                            item.Id = idItemBarNew;
                            return item;
                        }
                        break;
                    default:
                        break;
                }
                throw new InvalidOperationException();
            }));

            engine.Rename(CreateChangeRename(ChangeType.Edit | ChangeType.Rename, pathBar,      0, pathBarOld,  1, ItemType.File), true);
            engine.Rename(CreateChangeRename(ChangeType.Edit | ChangeType.Rename, pathBarNew,   0, pathBar,     1, ItemType.File), true);

            AssertFolder(root, "project", 0, 4);
            var items = root.Items;
            var elemItemVictim = items[0];
            var elemItemVictimRenamed = items[1];
            var elemItemVictimNew = items[2];
            var elemItemVictimRenamedNew = items[3];
            AssertDeleteItem(elemItemVictim, pathBar);
            AssertItem(elemItemVictimRenamed, pathBarOld, 1);
            Assert.Equal(elemItemVictimRenamed.Id, idItemBar);
            AssertDeleteItem(elemItemVictimNew, pathBarNew);
            AssertItem(elemItemVictimRenamedNew, pathBar, 1);
            Assert.Equal(elemItemVictimRenamedNew.Id, idItemBarNew);
        }

        [Fact(Skip="Temporary disable (not complete yet)")]
        [Trait("TestName", "BTCIMCCDES")]
        public void BrokenTFSCaseInsensitiveMismatchCommitContentDamageEnsureSanitized()
        {
            //System.Diagnostics.Debugger.Launch();
            string pathOther = "project/other";
            string pathOtherOther1 = pathOther + "/other1.txt"; // foreign file, properly existing
            string pathSub = "project/sub";
            string pathOtherOther1New = pathSub + "/other1.txt"; // incorrectly-cased (NON-EXISTING) destination directory
            string pathSUB = "project/SUB";
            string pathSUBfoo = pathSUB + "/foo.txt"; // properly existing file
            stub.Attach(sourceControlProvider.GetItems, Return.DelegateResult(delegate(object[] parameters)
            {
                int version = (int)parameters[0];
                string path = (string)parameters[1];
                //var recursion = parameters[2];

                switch (version)
                {
                    case 1:
                        if ((pathSUB == path) || (pathOther == path))
                        {
                            FolderMetaData folder = new FolderMetaData(path);
                            return folder;
                        }
                        else
                        if (pathSub == path)
                        {
                            return null; // invalid, non-existing sub dir - does not get retrieved
                        }
                        else
                        if (pathOtherOther1New == path)
                        {
                            // yup, this incorrectly-cased item *does* get retrieved...
                            ItemMetaData item = CreateItem(path, version);
                            return item;
                        }
                        else
                        if (pathSUBfoo == path)
                        {
                            ItemMetaData item = CreateItem(path, version);
                            return item;
                        }
                        break;
                    default:
                        break;
                }
                throw new InvalidOperationException();
            }));

            engine.Edit(CreateChange(ChangeType.Edit, pathSUBfoo, 1, ItemType.File));
            engine.Rename(CreateChangeRename(ChangeType.Edit | ChangeType.Rename, pathOther,  0, pathOtherOther1New,  1, ItemType.File), true);

            AssertFolder(root, "project", 0, 2);
            var items = root.Items;
            AssertFolder(items[0], pathSUB, 1, 2);
            AssertDeleteItem(items[1], pathOther);
            //AssertItem(items[1], pathBarOld, 1);
            //Assert.Equal(items[1].Id, idItemBar);
            //AssertDeleteItem(items[2], pathBarNew);
            //AssertItem(items[3], pathBar, 1);
            //Assert.Equal(items[3].Id, idItemBarNew);
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

        [DebuggerStepThrough]
        private SourceItemChange CreateChangeRename(ChangeType changeType, string originalRemoteName, int originalRevision, string remoteName, int changeset, ItemType itemType)
        {
            SourceItem sourceItemRenamed = new SourceItem();
            sourceItemRenamed.RemoteName = remoteName;
            sourceItemRenamed.RemoteChangesetId = changeset;
            sourceItemRenamed.ItemType = itemType;
            RenamedSourceItem renamedSourceItem = new RenamedSourceItem(sourceItemRenamed, originalRemoteName, originalRevision);
            SourceItemChange change = new SourceItemChange(renamedSourceItem, changeType);
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
