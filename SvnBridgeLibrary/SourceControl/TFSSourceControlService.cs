using System;
using System.Collections.Generic;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;
using System.Text;
using System.IO;
using SvnBridge.Interfaces;
using SvnBridge.Infrastructure;

namespace SvnBridge.SourceControl
{
	public class TFSSourceControlService : SourceControlService
	{
        private readonly DefaultLogger logger;
		private readonly IRepositoryWebSvcFactory webSvcFactory;

		public TFSSourceControlService(
			IRegistrationService registrationService, 
			IRepositoryWebSvcFactory webSvcFactory, 
			IWebTransferService webTransferService, 
			IFileSystem fileSystem,
            DefaultLogger logger)
			: base(registrationService, webSvcFactory, webTransferService, fileSystem)
		{
			this.webSvcFactory = webSvcFactory;
			this.logger = logger;
		}

		public ExtendedItem[][] QueryItemsExtended(string tfsUrl,
		                                           ICredentials credentials,
		                                           string workspaceName,
		                                           ItemSpec[] items,
		                                           DeletedState deletedState,
		                                           ItemType itemType)
		{
			Repository webSvc = CreateProxy(tfsUrl, credentials);
			string username = TfsUtil.GetUsername(credentials, tfsUrl);
			return webSvc.QueryItemsExtended(workspaceName, username, items, deletedState, itemType);
		}

		private Repository CreateProxy(string tfsUrl, ICredentials credentials)
		{
			return (Repository)webSvcFactory.Create(tfsUrl, credentials);
		}

		public BranchRelative[][] QueryBranches(string tfsUrl,
		                                        ICredentials credentials,
		                                        string workspaceName,
		                                        ItemSpec[] items,
		                                        VersionSpec version)
		{
			Repository webSvc = CreateProxy(tfsUrl, credentials);
			string username = TfsUtil.GetUsername(credentials, tfsUrl);
			return webSvc.QueryBranches(workspaceName, username, items, version);
		}

		public SourceItem QueryItems(string tfsUrl, ICredentials credentials, int itemIds, int changeSet)
		{
			SourceItem[] items = QueryItems(tfsUrl, credentials, new int[]{itemIds}, changeSet);
			if(items.Length==0)
				return null;
			return items[0];
		}

        public ItemSet[] QueryItems(string tfsUrl, ICredentials credentials, VersionSpec version, ItemSpec[] items)
        {
            Repository webSvc = CreateProxy(tfsUrl, credentials);
            string username = TfsUtil.GetUsername(credentials, tfsUrl);
            return webSvc.QueryItems(null, null, items, version, DeletedState.NonDeleted, ItemType.Any, true);
        }
    }
}
