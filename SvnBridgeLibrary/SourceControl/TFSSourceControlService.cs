using System;
using System.Net;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.ObjectModel; // LogItem
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;
using SvnBridge.Infrastructure; // DefaultLogger
using SvnBridge.Utility; // Helper.GetUnsafeNetworkCredential()

namespace SvnBridge.SourceControl
{
	public class TFSSourceControlService : SourceControlService
	{
        private readonly DefaultLogger logger;

        public TFSSourceControlService(
            IRegistrationService registrationService,
            IRepositoryWebSvcFactory webSvcFactory,
            IWebTransferService webTransferService,
            IFileSystem fileSystem,
            DefaultLogger logger)
			: base(
                registrationService,
                webSvcFactory,
                webTransferService,
                fileSystem)
		{
			this.logger = logger;
		}

        public override WorkspaceInfo[] GetWorkspaces(
            string tfsUrl,
            ICredentials credentials,
            WorkspaceComputers computers,
            int permissionsFilter)
        {
            try
            {
                return base.GetWorkspaces(
                    tfsUrl,
                    credentials,
                    computers,
                    permissionsFilter);
            }
            catch (Exception e)
            {
                if (e.Message.StartsWith("TF14002:")) // The identity is not a member of the Team Foundation Valid Users group.
                    throw new NetworkAccessDeniedException(e);

                throw;
            }
        }

		public ExtendedItem[][] QueryItemsExtended(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            ItemSpec[] items,
            DeletedState deletedState,
            ItemType itemType,
            int options)
		{
            return WrapWebException<ExtendedItem[][]>(delegate
            {
                using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                {
                    string username = TfsUtil.GetUsername(credentials, tfsUrl);
                    return webSvc.QueryItemsExtended(
                        workspaceName,
                        username,
                        items,
                        deletedState,
                        itemType,
                        options);
                }
            });
		}

		public BranchRelative[][] QueryBranches(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            ItemSpec[] items,
            VersionSpec version)
		{
            return WrapWebException<BranchRelative[][]>(delegate
            {
                using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                {
                    string username = TfsUtil.GetUsername(credentials, tfsUrl);
                    return webSvc.QueryBranches(
                        workspaceName,
                        username,
                        items,
                        version);
                }
            });
		}

        public virtual ItemSet[] QueryItems(
            string tfsUrl,
            ICredentials credentials,
            VersionSpec version,
            ItemSpec[] items,
            int options)
        {
            return WrapWebException(delegate
            {
                ItemSet[] result = null;
                using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                {
                    result = webSvc.QueryItems(
                        null,
                        null,
                        items,
                        version,
                        DeletedState.NonDeleted,
                        ItemType.Any,
                        true,
                        options);
                }

                // Make damn sure to keep second web service object instantiated below
                // instantiated within a cleanly separated _different_ scope
                // (i.e., original web service object above reliably Dispose()d),
                // to try to avoid (try hard to reduce likelihood of)
                // a "socket reuse" error exception:
                //
                // "A first chance exception of type 'System.Net.Sockets.SocketException' occurred in System.dll
                // Additional information: Only one usage of each socket address (protocol/network address/port) is normally permitted"
                //
                // However, a major cause of this problem AFAICS is that both current request handler and background worker thread
                // do TFS info requests, causing frequent use of ports.
                // Note that this problem can cause failing connection request to TFS which ripples through to SvnBridge client side
                // and makes client side sit up and take notice (read: FAIL!).
                // For a potential solution to this problem (TODO), see specific port range mechanism described at
                // "Only one usage of each socket address (protocol/network address/port) is normally permitted"
                //    http://blogs.msdn.com/b/dgorti/archive/2005/09/18/470766.aspx
                // ServicePointManager.ReusePort might be related to this, too.

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
                        using (Repository readAllWebSvc = CreateProxy(tfsUrl, readAllCredentials))
                        {
                            ItemSet[] readAllResult = readAllWebSvc.QueryItems(
                                null,
                                null,
                                items,
                                version,
                                DeletedState.NonDeleted,
                                ItemType.Any,
                                true,
                                options);
                            if (readAllResult[0].Items.Length == 0)
                                invalidPath = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Error connecting with read all account \"" + Configuration.ReadAllUserName + "\"", ex);
                    }

                    // This check will throw an "access denied"
                    // for *both* cases of
                    // - masked (null entries) access denied error of user account read attempt
                    // - access denied exception of read-all account read attempt
                    // One might be tempted to avoid throwing
                    // in case user result was empty
                    // yet verification via read-all then threw
                    // (e.g. due to read all account being *unconfigured*).
                    // However since read-all failing means that subsequent validation
                    // of the imprecise user account result
                    // (empty set due to *either* error *or* an actual empty set)
                    // was not possible,
                    // it's best to always throw an exception
                    // (thereby making the admin sit up and take
                    // notice of account problems)
                    // rather than continuing to forward an unverified item
                    // (potential problem) to client side
                    // (possibly causing improper tree state changes).
                    if (!invalidPath)
                        throw new NetworkAccessDeniedException();
                }
                return result;
            });
        }

        public Changeset[] QueryHistory(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            string workspaceOwner,
            ItemSpec itemSpec,
            VersionSpec versionItem,
            string user,
            VersionSpec versionFrom,
            VersionSpec versionTo,
            int maxCount,
            bool includeFiles,
            bool generateDownloadUrls,
            bool slotMode,
            bool sortAscending)
        {
            return WrapWebException<Changeset[]>(delegate
            {
                using (Repository webSvc = CreateProxy(tfsUrl, credentials))
                {
                    return webSvc.QueryHistory(
                        workspaceName,
                        workspaceOwner,
                        itemSpec,
                        versionItem,
                        user,
                        versionFrom,
                        versionTo,
                        maxCount,
                        includeFiles,
                        generateDownloadUrls,
                        slotMode,
                        sortAscending);
                }
            });
        }

        // NOTE: WrapWebException* are duplicated local implementations (i.e., copies)
        // of the *private* (read: unreachable) helpers
        // in the external-library base class.
        delegate T WrapWebExceptionDelegate<T>();

        // Please note that use of this method here
        // constitutes somewhat of a layer violation,
        // since *some* public methods of our (derived!) service class
        // do wrap things yet *other* *public* methods of the *base* service class
        // quite obviously *don't*.
        // The correct way to do things would probably be to correct TFSSourceControlService
        // to be an ISourceControlService-compatible *wrapper*(!)
        // around an internal SourceControlService *member*
        // (hmm, probably to be done via IInterceptor mechanism instead).
        static T WrapWebException<T>(WrapWebExceptionDelegate<T> function)
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

    /// <summary>
    /// Intended to provide various helpers
    /// which offer missing TfsLibrary object model functionality
    /// and are generic enough
    /// that they ought to have (could have?) been provided
    /// by the TfsLibrary project already.
    /// Since this class is about TfsLibrary parts,
    /// should try to have only helpers here
    /// which are limited to TfsLibrary-provided types,
    /// i.e. avoid foreign (e.g. SvnBridge) types.
    /// </summary>
    public class TfsLibraryHelpers
    {
        public static LogItem LogItemClone(LogItem logItem)
        {
            // Resort to open-coded ctor (for lack of a class-side .Clone()...):
            return new LogItem(logItem.LocalPath, logItem.ServerPath, logItem.History);
        }
    }
}
