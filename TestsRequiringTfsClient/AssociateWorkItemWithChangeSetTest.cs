using System;
using System.Net;
using IntegrationTests;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using SvnBridge.Infrastructure;
using SvnBridge.SourceControl;
using EndToEndTests;
using TestsRequiringTfsClient.Properties;
using Xunit;

namespace TestsRequiringTfsClient
{
    public class AssociateWorkItemWithChangeSetTest : IDisposable
    {
        private int workItemId;
        private int changesetId;
        private WorkItemStore store;
        private AuthenticateAsLowPrivilegeUser authenticateAsLowPrivilegeUser;

        public  AssociateWorkItemWithChangeSetTest()
        {
            authenticateAsLowPrivilegeUser = new AuthenticateAsLowPrivilegeUser(Settings.Default.NonAdminUserName,
                                                                                Settings.Default.NonAdminUserPassword,
                                                                                Settings.Default.NonAdminUserDomain);

            TeamFoundationServer server = TeamFoundationServerFactory.GetServer(Settings.Default.ServerUrl);
            store = (WorkItemStore)server.GetService(typeof(WorkItemStore));
            CreateWorkItemAndGetLatestChangeSet(out changesetId, out workItemId);
        }

        public void Dispose()
        {
            authenticateAsLowPrivilegeUser.Dispose();
        }

        public static void CreateWorkItemAndGetLatestChangeSet(out int latestChangeSetId, out int workItemId)
        {
            TeamFoundationServer server = TeamFoundationServerFactory.GetServer(Settings.Default.ServerUrl);
            WorkItemStore store = (WorkItemStore)server.GetService(typeof(WorkItemStore));
            Project project = store.Projects["SvnBridgeTesting"];

            WorkItemType wit = project.WorkItemTypes["Work Item"];
            WorkItem wi = new WorkItem(wit);

            wi.Title = "blah";
            wi.Description = "no";

            wi.Save();

            workItemId = wi.Id;
            VersionControlServer vcs = (VersionControlServer)server.GetService(typeof(VersionControlServer));
            latestChangeSetId = vcs.GetLatestChangesetId();
        }


        [Fact]
        public void CanAssociateWorkItemWithChangeSet()
        {
            AssociateWorkItemWithChangeSet associateWorkItemWithChangeSet =
                new AssociateWorkItemWithChangeSet(Settings.Default.ServerUrl, CredentialsHelper.DefaultCredentials);
            associateWorkItemWithChangeSet.Associate(workItemId, changesetId);
            associateWorkItemWithChangeSet.SetWorkItemFixed(workItemId);

            WorkItem item = store.GetWorkItem(workItemId);
            Assert.Equal(1, item.Links.Count);
            Assert.Equal("Fixed", item.State);
            Assert.Equal("Fixed", item.Reason);
        }

        [Fact]
        public void CanAssociateWithWorkItemAfterWorkItemHasBeenModified()
        {
            AssociateWorkItemWithChangeSet associateWorkItemWithChangeSet =
               new AssociateWorkItemWithChangeSet(Settings.Default.ServerUrl, CredentialsHelper.DefaultCredentials);

            WorkItem item = store.GetWorkItem(workItemId);
            item.History = "test foo";
            item.Save();

            Assert.Equal(2, item.Revision);

            associateWorkItemWithChangeSet.Associate(workItemId, changesetId);
            associateWorkItemWithChangeSet.SetWorkItemFixed(workItemId);

            item = store.GetWorkItem(workItemId);
            Assert.Equal(1, item.Links.Count);
            Assert.Equal("Fixed", item.State);
            Assert.Equal("Fixed", item.Reason);
        }
    }
}
