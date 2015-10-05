using System;
using System.Collections.Generic;
using System.Text;
using SvnBridge.SourceControl;
using CodePlex.TfsLibrary.ObjectModel;
using System.Net;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Attach;

namespace UnitTests
{
    public class StubTFSSourceControlService : TFSSourceControlService
    {
        public List<SourceItem> QueryItems_Return = new List<SourceItem>();
        public ReturnDelegateResult.DelegateActionDelegate QueryItems_ReturnDelegate = null;

        public StubTFSSourceControlService() : base(null, null, null, null, null) { }

        public override SourceItem[] QueryItems(string tfsUrl, ICredentials credentials, string serverPath, RecursionType recursion, VersionSpec version, DeletedState deletedState, ItemType itemType, bool sortAscending, int options)
        {
            if (QueryItems_ReturnDelegate != null)
                return (SourceItem[])QueryItems_ReturnDelegate(new object[] { tfsUrl, credentials, serverPath, recursion, version, deletedState, itemType, sortAscending, options });

            return QueryItems_Return.ToArray();
        }
    }
}
