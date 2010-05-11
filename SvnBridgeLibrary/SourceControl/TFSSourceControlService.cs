using System;
using System.Collections.Generic;
using System.Net;
using System.Web.Services.Protocols;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;
using System.Text;
using System.IO;
using SvnBridge.Interfaces;
using SvnBridge.Infrastructure;
using CodePlex.TfsLibrary;

namespace SvnBridge.SourceControl
{
	public class TFSSourceControlService : SourceControlService
	{
        private readonly DefaultLogger logger;
        private readonly IRepositoryWebSvcFactory webSvcFactory;

        public TFSSourceControlService(IRegistrationService registrationService, IRepositoryWebSvcFactory webSvcFactory, IWebTransferService webTransferService, IFileSystem fileSystem, DefaultLogger logger)
			: base(registrationService, webSvcFactory, webTransferService, fileSystem)
		{
			this.webSvcFactory = webSvcFactory;
			this.logger = logger;
		}

        public override WorkspaceInfo[] GetWorkspaces(string tfsUrl, ICredentials credentials, WorkspaceComputers computers)
        {
            try
            {
                return base.GetWorkspaces(tfsUrl, credentials, computers);
            }
            catch (Exception e)
            {
                if (e.Message.StartsWith("TF14002:")) // The identity is not a member of the Team Foundation Valid Users group.
                    throw new NetworkAccessDeniedException(e);

                throw;
            }
        }

		public ExtendedItem[][] QueryItemsExtended(string tfsUrl, ICredentials credentials, string workspaceName, ItemSpec[] items, DeletedState deletedState, ItemType itemType)
		{
            return WrapWebException<ExtendedItem[][]>(delegate
            {
                Repository webSvc = CreateProxy(tfsUrl, credentials);
                string username = TfsUtil.GetUsername(credentials, tfsUrl);
                return webSvc.QueryItemsExtended(workspaceName, username, items, deletedState, itemType);
            });
		}

		public BranchRelative[][] QueryBranches(string tfsUrl, ICredentials credentials, string workspaceName, ItemSpec[] items, VersionSpec version)
		{
            return WrapWebException<BranchRelative[][]>(delegate
            {
                Repository webSvc = CreateProxy(tfsUrl, credentials);
                string username = TfsUtil.GetUsername(credentials, tfsUrl);
                return webSvc.QueryBranches(workspaceName, username, items, version);
            });
		}

		public SourceItem QueryItems(string tfsUrl, ICredentials credentials, int itemIds, int changeSet)
		{
            return WrapWebException<SourceItem>(delegate
            {
                SourceItem[] items = QueryItems(tfsUrl, credentials, new int[] { itemIds }, changeSet);
                if (items.Length == 0)
                    return null;
                return items[0];
            });
		}

        public virtual ItemSet[] QueryItems(string tfsUrl, ICredentials credentials, VersionSpec version, ItemSpec[] items)
        {
            return WrapWebException(delegate
            {
                Repository webSvc = CreateProxy(tfsUrl, credentials);
                ItemSet[] result = webSvc.QueryItems(null, null, items, version, DeletedState.NonDeleted, ItemType.Any, true);
                if (result[0].Items.Length == 0)
                {
                    // Check if no items returned due to no permissions.  
                    var badPath = false;
                    try
                    {
                        webSvc.QueryHistory(null, null, items[0], version, null, VersionSpec.First, version, 1, false, false, false);
                    }
                    catch (SoapException)
                    {
                        // For TFS08 and earlier, QueryHistory faults for bad path.
                        badPath = true;
                    }

                    if (!badPath && !string.IsNullOrEmpty(Configuration.ReadAllUserName))
                    {
                        try
                        {
                            Repository readAllWebSvc = CreateProxy(tfsUrl, new NetworkCredential(Configuration.ReadAllUserName, Configuration.ReadAllUserPassword, Configuration.ReadAllUserDomain));
                            ItemSet[] readAllResult = readAllWebSvc.QueryItems(null, null, items, version, DeletedState.NonDeleted, ItemType.Any, true);
                            if (readAllResult[0].Items.Length == 0)
                                badPath = true;
                        }
                        catch (Exception ex)
                        {
                            logger.Error("Error connecting with read all account " + Configuration.ReadAllUserName, ex);
                        }
                    }

                    if (!badPath)
                        throw new NetworkAccessDeniedException();
                }
                return result;
            });
        }

        public Changeset[] QueryHistory(string tfsUrl, ICredentials credentials, string workspaceName, string workspaceOwner, ItemSpec itemSpec, VersionSpec versionItem, string user, VersionSpec versionFrom, VersionSpec versionTo, int maxCount, bool includeFiles, bool generateDownloadUrls, bool slotMode)
        {
            return WrapWebException<Changeset[]>(delegate
            {
                Repository webSvc = CreateProxy(tfsUrl, credentials);
                return webSvc.QueryHistory(workspaceName, workspaceOwner, itemSpec, versionItem, user, versionFrom, versionTo, maxCount, includeFiles, generateDownloadUrls, slotMode);
            });
        }

        private Repository CreateProxy(string tfsUrl, ICredentials credentials)
        {
            return (Repository)webSvcFactory.Create(tfsUrl, credentials);
        }

        private delegate T WrapWebExceptionDelegate<T>();

        private T WrapWebException<T>(WrapWebExceptionDelegate<T> function)
        {
            try
            {
                return function();
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;

                if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new NetworkAccessDeniedException(ex);

                throw;
            }
        }
    }
}
