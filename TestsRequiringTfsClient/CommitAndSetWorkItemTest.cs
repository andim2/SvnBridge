using System.IO;
using IntegrationTests;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using EndToEndTests;
using TestsRequiringTfsClient.Properties;
using Xunit;

namespace TestsRequiringTfsClient
{
    public class CommitAndSetWorkItemTest : EndToEndTestBase
    {
        private int workItemId;
        private WorkItemStore store;
        private AuthenticateAsLowPrivilegeUser authenticateAsLowPrivilegeUser;

        public CommitAndSetWorkItemTest()
        {
            authenticateAsLowPrivilegeUser = new AuthenticateAsLowPrivilegeUser(Settings.Default.NonAdminUserName,
                                                                              Settings.Default.NonAdminUserPassword,
                                                                              Settings.Default.NonAdminUserDomain);

        
            TeamFoundationServer server = TeamFoundationServerFactory.GetServer(Settings.Default.ServerUrl);
            store = (WorkItemStore) server.GetService(typeof (WorkItemStore));
            int latestChangeSetId;
            TfsWorkItemModifierTest.CreateWorkItemAndGetLatestChangeSet(out latestChangeSetId, out workItemId);
        }

        public override void Dispose()
        {
            base.Dispose();
            authenticateAsLowPrivilegeUser.Dispose();
        }

        [SvnBridgeFact]
        public void CanFixWorkItemByCommitMessage()
        {
            CheckoutAndChangeDirectory();

            File.WriteAllText("test.txt", "blah");

            Svn("add test.txt");

            Svn("commit -m \"Done. Work Item: " + workItemId + "\"");

            WorkItem item = store.GetWorkItem(workItemId);
            Assert.Equal("Fixed", item.State);
            Assert.Equal("Fixed", item.Reason);
        }

        [SvnBridgeFact]
        public void AssociateTwoChangesetsWithWorkItem()
        {
            CheckoutAndChangeDirectory();

            File.WriteAllText("test.txt", "blah");

            Svn("add test.txt");

            Svn("commit -m \"Done. Work Item: " + workItemId + "\"");

            File.WriteAllText("test.txt", "hlab");

            Svn("commit -m \"Done. Work Item: " + workItemId + "\"");

            WorkItem item = store.GetWorkItem(workItemId);
            Assert.Equal(2, item.Links.Count);
            Assert.Equal("Fixed", item.State);
            Assert.Equal("Fixed", item.Reason);
        }

        [SvnBridgeFact]
        public void AssociatingSingleCheckInWithMultipleWorkItems()
        {
            int oldWorkItemId = workItemId;
            int latestChangeSetId;
            TfsWorkItemModifierTest.CreateWorkItemAndGetLatestChangeSet(out latestChangeSetId, out workItemId);
            string workItems = oldWorkItemId + ", " + workItemId;

            CheckoutAndChangeDirectory();

            File.WriteAllText("test.txt", "blah");

            Svn("add test.txt");

            Svn("commit -m \"Done. Work Item: " + workItems + "\"");

            foreach (int workItem in new int[] {oldWorkItemId, workItemId})
            {
                WorkItem item = store.GetWorkItem(workItem);
                Assert.Equal(1, item.Links.Count);
                Assert.Equal("Fixed", item.State);
                Assert.Equal("Fixed", item.Reason);
            }
        }
    }
}
