using System;
using System.Collections.Generic;
using CodePlex.TfsLibrary.ObjectModel;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using Xunit;
using Tests;
using Attach;

namespace SvnBridge
{
    public class CachePrePopulatorTest
    {
        private readonly MyMocks stubs = new MyMocks();
        private readonly CachePrePopulator cachePopulator;
        private readonly TFSSourceControlProvider sourceControlProvider;

        public CachePrePopulatorTest()
        {
            sourceControlProvider = stubs.CreateTFSSourceControlProviderStub();
            cachePopulator = new CachePrePopulator(sourceControlProvider);
        }

        [Fact]
        public void WillCallGetItemsOnSeparateItemsInTheCache()
        {
            var history = new SourceItemHistory
            {
                Changes = new List<SourceItemChange>
                {
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo"}},
                }
            };
            Results r1 = stubs.Attach(sourceControlProvider.GetItems, Return.Value(null));

            cachePopulator.PrePopulateCacheWithChanges(history, 15);

            Assert.Equal(15, r1.Parameters[0]);
            Assert.Equal("$/SvnBridge/foo", r1.Parameters[1]);
            Assert.Equal(Recursion.Full, r1.Parameters[2]);
        }


        [Fact]
        public void WillNotCallToChildrenOfItemAlreadyLoaded()
        {
            var history = new SourceItemHistory
            {
                Changes = new List<SourceItemChange>
                {
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo/1"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo/1/2"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo/1/2/3"}},
                }
            };
            Results r1 = stubs.Attach(sourceControlProvider.GetItems, Return.Value(null));

            cachePopulator.PrePopulateCacheWithChanges(history, 15);

            Assert.Equal(15, r1.Parameters[0]);
            Assert.Equal("$/SvnBridge/foo", r1.Parameters[1]);
            Assert.Equal(Recursion.Full, r1.Parameters[2]);
        }

        [Fact]
        public void WillNotCallToChildren_WhenFolderDepthIsGreaterThanOne()
        {
            var history = new SourceItemHistory
            {
                Changes = new List<SourceItemChange>
                {
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo/1/2/3"}},
                }
            };
            Results r1 = stubs.Attach(sourceControlProvider.GetItems, Return.Value(null));

            cachePopulator.PrePopulateCacheWithChanges(history, 15);

            Assert.Equal(15, r1.Parameters[0]);
            Assert.Equal("$/SvnBridge/foo", r1.Parameters[1]);
            Assert.Equal(Recursion.Full, r1.Parameters[2]);
        }


        [Fact]
        public void WillMergeHierarchyCallToCallsToOneParent()
        {
            var history = new SourceItemHistory
            {
                Changes = new List<SourceItemChange>
                {
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo/1"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo/1/2"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo/1/2/3"}},
                }
            };
            Results r1 = stubs.Attach(sourceControlProvider.GetItems, Return.Value(null));

            cachePopulator.PrePopulateCacheWithChanges(history, 15);

            Assert.Equal(15, r1.Parameters[0]);
            Assert.Equal("$/SvnBridge/foo", r1.Parameters[1]);
            Assert.Equal(Recursion.Full, r1.Parameters[2]);
        }

        [Fact]
        public void WillMergeManyCallsOfSubFoldersToCallsToOneParent()
        {
            var history = new SourceItemHistory
            {
                Changes = new List<SourceItemChange>
                {
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/bar"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/fubar"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/baz"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/bay"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/fey"}},
                }
            };
            Results r1 = stubs.Attach(sourceControlProvider.GetItems, Return.Value(null));

            cachePopulator.PrePopulateCacheWithChanges(history, 15);

            Assert.Equal(15, r1.Parameters[0]);
            Assert.Equal("$/SvnBridge", r1.Parameters[1]);
            Assert.Equal(Recursion.Full, r1.Parameters[2]);
        }

        [Fact]
        public void WillNotMergeFewCallsOfSubFoldersToCallsToOneParent()
        {
            var history = new SourceItemHistory
            {
                Changes = new List<SourceItemChange>
                {
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/foo"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/bar"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/fubar"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge/baz"}},
                }
            };
            Results r1 = stubs.Attach(sourceControlProvider.GetItems, Return.Value(null));

            cachePopulator.PrePopulateCacheWithChanges(history, 15);

            Assert.Equal(15, r1.History[0].Parameters[0]);
            Assert.Equal("$/SvnBridge/foo", r1.History[0].Parameters[1]);
            Assert.Equal(Recursion.Full, r1.History[0].Parameters[2]);
            Assert.Equal(15, r1.History[1].Parameters[0]);
            Assert.Equal("$/SvnBridge/bar", r1.History[1].Parameters[1]);
            Assert.Equal(Recursion.Full, r1.History[1].Parameters[2]);
            Assert.Equal(15, r1.History[2].Parameters[0]);
            Assert.Equal("$/SvnBridge/fubar", r1.History[2].Parameters[1]);
            Assert.Equal(Recursion.Full, r1.History[2].Parameters[2]);
            Assert.Equal(15, r1.History[3].Parameters[0]);
            Assert.Equal("$/SvnBridge/baz", r1.History[3].Parameters[1]);
            Assert.Equal(Recursion.Full, r1.History[3].Parameters[2]);
        }

        [Fact]
        public void WillNotMergeManyCallsToDifferentProjects()
        {
            var history = new SourceItemHistory
            {
                Changes = new List<SourceItemChange>
                {
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge1"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge2"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge3"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge4"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge5"}},
                    new SourceItemChange {Item = new SourceItem {RemoteName = "$/SvnBridge6"}},
                }
            };
            Results r1 = stubs.Attach(sourceControlProvider.GetItems, Return.Value(null));

            cachePopulator.PrePopulateCacheWithChanges(history, 15);

            for (int i = 0; i < 6; i++)
            {
                Assert.Equal(15, r1.History[i].Parameters[0]);
                Assert.Equal("$/SvnBridge" + (i + 1), r1.History[i].Parameters[1]);
                Assert.Equal(Recursion.Full, r1.History[i].Parameters[2]);
            }
        }
    }
}