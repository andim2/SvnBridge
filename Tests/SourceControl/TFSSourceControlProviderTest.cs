using System;
using CodePlex.TfsLibrary.ObjectModel;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using Tests;
using SvnBridge.Cache;
using Attach;
using System.Net;
using SvnBridge.SourceControl;

namespace UnitTests
{
    public class TFSSourceControlProviderTest
    {
        private readonly MyMocks stubs;
        private readonly TfsWorkItemModifier associateWorkItemWithChangeSet;
        private readonly TFSSourceControlProvider provider;

        public TFSSourceControlProviderTest()
        {
            stubs = new MyMocks();
            associateWorkItemWithChangeSet = stubs.CreateObject<TfsWorkItemModifier>("http://www.codeplex.com", null);
            provider = new TFSSourceControlProvider(
                "http://www.blah.com",
                null,
				null,
                new StubTFSSourceControlService(),
                associateWorkItemWithChangeSet,
                stubs.CreateObject<DefaultLogger>(),
                stubs.CreateObject<WebCache>(),
                stubs.CreateObject<FileRepository>("http://www.codeplex.com", null, null));
        }

        [Fact]
        public void WillNotAssociateIfCommentHasNoWorkItems()
        {
            Results r1 = stubs.Attach(associateWorkItemWithChangeSet.Associate);
            Results r2 = stubs.Attach(associateWorkItemWithChangeSet.SetWorkItemFixed);

            provider.AssociateWorkItemsWithChangeSet("blah blah", 15);

            Assert.False(r1.WasCalled);
            Assert.False(r2.WasCalled);
        }

        [Fact]
        public void WillExtractWorkItemsFromCheckInCommentsAndAssociateWithChangeSet()
        {
            Results r1 = stubs.Attach(associateWorkItemWithChangeSet.Associate);
            Results r2 = stubs.Attach(associateWorkItemWithChangeSet.SetWorkItemFixed);
            string comment = @"blah blah
Work Item: 15";

            provider.AssociateWorkItemsWithChangeSet(comment, 15);

            Assert.Equal(15, r1.Parameters[0]);
            Assert.Equal(15, r1.Parameters[1]);
            Assert.Equal(15, r2.Parameters[0]);
        }

        [Fact]
        public void CanAssociateMoreThanOneId()
        {
            Results r1 = stubs.Attach(associateWorkItemWithChangeSet.Associate);
            Results r2 = stubs.Attach(associateWorkItemWithChangeSet.SetWorkItemFixed);
            string comment = @"blah blah
Work Items: 15, 16, 17";

            provider.AssociateWorkItemsWithChangeSet(comment, 15);

            Assert.Equal(15, r1.History[0].Parameters[0]);
            Assert.Equal(15, r1.History[0].Parameters[1]);
            Assert.Equal(16, r1.History[1].Parameters[0]);
            Assert.Equal(15, r1.History[1].Parameters[1]);
            Assert.Equal(17, r1.History[2].Parameters[0]);
            Assert.Equal(15, r1.History[2].Parameters[1]);
            Assert.Equal(15, r2.History[0].Parameters[0]);
            Assert.Equal(16, r2.History[1].Parameters[0]);
            Assert.Equal(17, r2.History[2].Parameters[0]);
        }

        [Fact]
        public void CanAssociateOnMultipleLines()
        {
            Results r1 = stubs.Attach(associateWorkItemWithChangeSet.Associate);
            Results r2 = stubs.Attach(associateWorkItemWithChangeSet.SetWorkItemFixed);
            string comment = @"blah blah
Work Items: 15, 16
Work Item: 17";

            provider.AssociateWorkItemsWithChangeSet(comment, 15);

            Assert.Equal(15, r1.History[0].Parameters[0]);
            Assert.Equal(15, r1.History[0].Parameters[1]);
            Assert.Equal(16, r1.History[1].Parameters[0]);
            Assert.Equal(15, r1.History[1].Parameters[1]);
            Assert.Equal(17, r1.History[2].Parameters[0]);
            Assert.Equal(15, r1.History[2].Parameters[1]);
            Assert.Equal(15, r2.History[0].Parameters[0]);
            Assert.Equal(16, r2.History[1].Parameters[0]);
            Assert.Equal(17, r2.History[2].Parameters[0]);
        }

        [Fact]
        public void WillRecognizeWorkItemsIfWorkItemAppearsPreviouslyInText()
        {
            Results r1 = stubs.Attach(associateWorkItemWithChangeSet.Associate);
            Results r2 = stubs.Attach(associateWorkItemWithChangeSet.SetWorkItemFixed);
            string comment = @"Adding work items support and fixing
other issues with workitems
Solved Work Items: 15, 16
Fixed WorkItem: 17
Assoicate with workitem: 81";

            provider.AssociateWorkItemsWithChangeSet(comment, 15);

            Assert.Equal(15, r1.History[0].Parameters[0]);
            Assert.Equal(15, r1.History[0].Parameters[1]);
            Assert.Equal(16, r1.History[1].Parameters[0]);
            Assert.Equal(15, r1.History[1].Parameters[1]);
            Assert.Equal(17, r1.History[2].Parameters[0]);
            Assert.Equal(15, r1.History[2].Parameters[1]);
            Assert.Equal(81, r1.History[3].Parameters[0]);
            Assert.Equal(15, r1.History[3].Parameters[1]);
            Assert.Equal(15, r2.History[0].Parameters[0]);
            Assert.Equal(16, r2.History[1].Parameters[0]);
            Assert.Equal(17, r2.History[2].Parameters[0]);
            Assert.Equal(81, r2.History[3].Parameters[0]);
        }

        [Fact]
        public void CanUseResolveAndClosesSyntaxToAssociateMultipleWorkItems()
        {
            Results r1 = stubs.Attach(associateWorkItemWithChangeSet.Associate);
            Results r2 = stubs.Attach(associateWorkItemWithChangeSet.SetWorkItemFixed);
            string comment = @"blah blah
resolve: #15, 16
closes 17";

            provider.AssociateWorkItemsWithChangeSet(comment, 15);

            Assert.Equal(15, r1.History[0].Parameters[0]);
            Assert.Equal(15, r1.History[0].Parameters[1]);
            Assert.Equal(16, r1.History[1].Parameters[0]);
            Assert.Equal(15, r1.History[1].Parameters[1]);
            Assert.Equal(17, r1.History[2].Parameters[0]);
            Assert.Equal(15, r1.History[2].Parameters[1]);
            Assert.Equal(15, r2.History[0].Parameters[0]);
            Assert.Equal(16, r2.History[1].Parameters[0]);
            Assert.Equal(17, r2.History[2].Parameters[0]);
        }

        [Fact]
        public void CanAssociateMoreThanOneIdWithHashes()
        {
            Results r1 = stubs.Attach(associateWorkItemWithChangeSet.Associate);
            Results r2 = stubs.Attach(associateWorkItemWithChangeSet.SetWorkItemFixed);
            string comment = @"blah blah
Work Items: 15, 16, #17";

            provider.AssociateWorkItemsWithChangeSet(comment, 15);

            Assert.Equal(15, r1.History[0].Parameters[0]);
            Assert.Equal(15, r1.History[0].Parameters[1]);
            Assert.Equal(16, r1.History[1].Parameters[0]);
            Assert.Equal(15, r1.History[1].Parameters[1]);
            Assert.Equal(17, r1.History[2].Parameters[0]);
            Assert.Equal(15, r1.History[2].Parameters[1]);
            Assert.Equal(15, r2.History[0].Parameters[0]);
            Assert.Equal(16, r2.History[1].Parameters[0]);
            Assert.Equal(17, r2.History[2].Parameters[0]);
        }

        [Fact]
        public void CanAssociateMoreThanOneIdWithHashesAndFixSyntax()
        {
            Results r1 = stubs.Attach(associateWorkItemWithChangeSet.Associate);
            Results r2 = stubs.Attach(associateWorkItemWithChangeSet.SetWorkItemFixed);
            string comment = @"blah blah
fix 15, 16, #17";

            provider.AssociateWorkItemsWithChangeSet(comment, 15);

            Assert.Equal(15, r1.History[0].Parameters[0]);
            Assert.Equal(15, r1.History[0].Parameters[1]);
            Assert.Equal(16, r1.History[1].Parameters[0]);
            Assert.Equal(15, r1.History[1].Parameters[1]);
            Assert.Equal(17, r1.History[2].Parameters[0]);
            Assert.Equal(15, r1.History[2].Parameters[1]);
            Assert.Equal(15, r2.History[0].Parameters[0]);
            Assert.Equal(16, r2.History[1].Parameters[0]);
            Assert.Equal(17, r2.History[2].Parameters[0]);
        }

    }
}
