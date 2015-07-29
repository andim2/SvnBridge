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
using SvnBridge.Utility; // Helper.GetUnsafeNetworkCredential()
using CodePlex.TfsLibrary;

namespace SvnBridge.SourceControl
{
	public class TFSSourceControlService : SourceControlService
	{
        private readonly DefaultLogger logger;

        public TFSSourceControlService(IRegistrationService registrationService, IRepositoryWebSvcFactory webSvcFactory, IWebTransferService webTransferService, IFileSystem fileSystem, DefaultLogger logger)
			: base(registrationService, webSvcFactory, webTransferService, fileSystem)
		{
			this.logger = logger;
		}

        public override WorkspaceInfo[] GetWorkspaces(string tfsUrl, ICredentials credentials, WorkspaceComputers computers, int permissionsFilter)
        {
            try
            {
                return base.GetWorkspaces(tfsUrl, credentials, computers, permissionsFilter);
            }
            catch (Exception e)
            {
                if (e.Message.StartsWith("TF14002:")) // The identity is not a member of the Team Foundation Valid Users group.
                    throw new NetworkAccessDeniedException(e);

                throw;
            }
        }

		public ExtendedItem[][] QueryItemsExtended(string tfsUrl, ICredentials credentials, string workspaceName, ItemSpec[] items, DeletedState deletedState, ItemType itemType, int options)
		{
            return WrapWebException<ExtendedItem[][]>(delegate
            {
                using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                {
                    string username = TfsUtil.GetUsername(credentials, tfsUrl);
                    return webSvc.QueryItemsExtended(workspaceName, username, items, deletedState, itemType, options);
                }
            });
		}

		public BranchRelative[][] QueryBranches(string tfsUrl, ICredentials credentials, string workspaceName, ItemSpec[] items, VersionSpec version)
		{
            return WrapWebException<BranchRelative[][]>(delegate
            {
                using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                {
                    string username = TfsUtil.GetUsername(credentials, tfsUrl);
                    return webSvc.QueryBranches(workspaceName, username, items, version);
                }
            });
		}

		public SourceItem QueryItems(string tfsUrl, ICredentials credentials, int itemIds, int changeSet, int options)
		{
            return WrapWebException<SourceItem>(delegate
            {
                SourceItem[] items = QueryItems(tfsUrl, credentials, new int[] { itemIds }, changeSet, options);
                if (items.Length == 0)
                    return null;
                return items[0];
            });
		}

        public virtual ItemSet[] QueryItems(string tfsUrl, ICredentials credentials, VersionSpec version, ItemSpec[] items, int options)
        {
            return WrapWebException(delegate
            {
                ItemSet[] result = null;
                using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                {
                    result = webSvc.QueryItems(null, null, items, version, DeletedState.NonDeleted, ItemType.Any, true, options);
                }
                if (result[0].Items.Length == 0)
                {
                    // Check if no items returned due to no permissions.
                    var invalidPath = false;

                    NetworkCredential readAllCredentials = Helper.GetUnsafeNetworkCredential();
                    if (!string.IsNullOrEmpty(Configuration.ReadAllUserName))
                    {
                        readAllCredentials = new NetworkCredential(Configuration.ReadAllUserName, Configuration.ReadAllUserPassword, Configuration.ReadAllUserDomain);
                    }
                    try
                    {
                        Repository readAllWebSvc = CreateProxy(tfsUrl, readAllCredentials);
                        ItemSet[] readAllResult = readAllWebSvc.QueryItems(null, null, items, version, DeletedState.NonDeleted, ItemType.Any, true, options);
                        if (readAllResult[0].Items.Length == 0)
                            invalidPath = true;
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Error connecting with read all account " + Configuration.ReadAllUserName, ex);
                    }

                    if (!invalidPath)
                        throw new NetworkAccessDeniedException();
                }
                return result;
            });
        }

        public Changeset[] QueryHistory(string tfsUrl, ICredentials credentials, string workspaceName, string workspaceOwner, ItemSpec itemSpec, VersionSpec versionItem, string user, VersionSpec versionFrom, VersionSpec versionTo, int maxCount, bool includeFiles, bool generateDownloadUrls, bool slotMode, bool sortAscending)
        {
            return WrapWebException<Changeset[]>(delegate
            {
                using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                {
                    return webSvc.QueryHistory(workspaceName, workspaceOwner, itemSpec, versionItem, user, versionFrom, versionTo, maxCount, includeFiles, generateDownloadUrls, slotMode, sortAscending);
                }
            });
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
