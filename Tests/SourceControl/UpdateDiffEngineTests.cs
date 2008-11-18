using System;
using System.Collections.Generic;
using System.Text;
using SvnBridge.SourceControl;
using Xunit;
using CodePlex.TfsLibrary.ObjectModel;
using Tests;
using Attach;

namespace UnitTests.SourceControl
{
    public class UpdateDiffEngineTests
    {
        MyMocks stub = new MyMocks();
        FolderMetaData root = new FolderMetaData("/project");
        string checkoutRootPath = "";
        int targetVersion = 1;
        TFSSourceControlProvider sourceControlProvider = null;
        Dictionary<string, int> clientExistingFiles = new Dictionary<string, int>();
        Dictionary<string, string> clientMissingFiles = new Dictionary<string, string>();
        List<string> renamedItemsToBeCheckedForDeletedChildren = new List<string>();
        Dictionary<ItemMetaData, bool> additionForPropertyChangeOnly = new Dictionary<ItemMetaData, bool>();

        SourceItemChange change;
        UpdateDiffEngine engine;

        public UpdateDiffEngineTests()
        {
            sourceControlProvider = stub.CreateTFSSourceControlProviderStub();
            engine = new UpdateDiffEngine(root, checkoutRootPath, targetVersion, sourceControlProvider, clientExistingFiles, clientMissingFiles, additionForPropertyChangeOnly, renamedItemsToBeCheckedForDeletedChildren);
        }

        [Fact]
        public void Add_File()
        {
            SourceItem sourceItem = new SourceItem();
            sourceItem.RemoteName = "project/file.txt";
            change = new SourceItemChange(sourceItem, CodePlex.TfsLibrary.RepositoryWebSvc.ChangeType.Add);
            ItemMetaData item = new ItemMetaData("/project/file.txt");
            stub.Attach(sourceControlProvider.GetItems, Return.Value(item));

            engine.Add(change);

            Assert.Equal("/project", root.Name);
            Assert.Equal(1, root.Items.Count);
            Assert.Equal("/project/file.txt", root.Items[0].Name);
        }
    }
}
