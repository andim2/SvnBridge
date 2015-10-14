using System;
using System.Diagnostics; // Conditional
using System.Net;
using CodePlex.TfsLibrary;
using CodePlex.TfsLibrary.ObjectModel; // LogItem
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;
using SvnBridge.Infrastructure; // DefaultLogger
using SvnBridge.Interfaces; // ITFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Exception_NeedSanitize
using SvnBridge.Utility; // Helper.DebugUsefulBreakpointLocation(), Helper.GetUnsafeNetworkCredential()

namespace SvnBridge.SourceControl
{
    // FIXME: these are using:s required for ITFSSourceControlService_wrapper-related types...
    using System.IO; // Stream
    using System.Collections.Generic; // IEnumerable

    /// <summary>
    /// Simple wrapper to provide the externally used class
    /// with its correct name and full class-specific parameter set,
    /// at a generic ITFSSourceControlService interface.
    /// </summary>
    public class TFSSourceControlService : ITFSSourceControlService_wrapper
    {
        public TFSSourceControlService(
            IRegistrationService registrationService,
            IRepositoryWebSvcFactory webSvcFactory,
            IWebTransferService webTransferService,
            IFileSystem fileSystem,
            DefaultLogger logger)
            : base(
                ConstructWrappedSourceControlService(
                    registrationService,
                    webSvcFactory,
                    webTransferService,
                    fileSystem,
                    logger))
        {
        }

        /// <summary>
        /// ITFSSourceControlService-constructing helper (wrapper chain).
        /// Required in order to be able to properly serve
        /// the *ctor-side* chaining mechanism
        /// as provided by the ITFSSourceControlService_wrapper.
        /// </summary>
        private static ITFSSourceControlService ConstructWrappedSourceControlService(
            IRegistrationService registrationService,
            IRepositoryWebSvcFactory webSvcFactory,
            IWebTransferService webTransferService,
            IFileSystem fileSystem,
            DefaultLogger logger)
        {
            ITFSSourceControlService scs_wrapper_outermost = null;

            bool useTfsService = true;
            if (useTfsService)
            {
                ITFSSourceControlService scs_buggy = new TFSSourceControlService_buggy_database_content(
                    registrationService,
                    webSvcFactory,
                    webTransferService,
                    fileSystem,
                    logger);
                scs_wrapper_outermost = scs_buggy;
            }
            else
            {
                // Well, what kind of emulation service or other
                // would one want to offer here??
            }
            bool doSanitize_BugAll = (Configuration.TfsCorrectCaseSensitivityInconsistentCommitRecords);
            if (doSanitize_BugAll)
            {
                ITFSSourceControlService scs_wrapper_bug_sanitizer = new TFSSourceControlService_BugSanitizerAll(
                    scs_wrapper_outermost);
                scs_wrapper_outermost = scs_wrapper_bug_sanitizer;
            }
            // For the cases where we enable a statistics counter,
            // this should always remain at the outermost wrapper layer,
            // right in front of the interface user which submits requests.
            bool statistics_count_requests = false;
            if (statistics_count_requests)
            {
                ITFSSourceControlService scs_wrapper_statistics_count_requests = new TFSSourceControlService_Statistics_CountRequests(scs_wrapper_outermost);
                scs_wrapper_outermost = scs_wrapper_statistics_count_requests;
            }

            return scs_wrapper_outermost;
        }
    }

    /// <summary>
    /// Interface chaining wrapper helper class.
    /// To be used as a base for purposes such as
    /// - augmenting interface activity (e.g. statistics)
    /// - filtering / sanitizing incorrect interface-provided raw data
    /// </summary>
    /// <remarks>
    /// Note: I decided to do full interface-wrapping forwarders
    /// (which we can wrap/stack fully runtime-dynamically,
    /// i.e. construct a full *chain* of interface implementers!!)
    /// rather than alternative possibilities such as:
    /// - interception (which is said to have a *huge* performance penalty)
    ///
    /// "C# Interface Based Development" http://www.c-sharpcorner.com/UploadFile/rmcochran/csharp_interrfaces03052006095933AM/csharp_interrfaces.aspx?ArticleID=cd6a6952-530a-4250-a6d7-5%3Ccode%3E%3Ca%20href=
    /// http://stackoverflow.com/questions/8387004/how-to-make-a-simple-dynamic-proxy-in-c-sharp
    ///
    /// This particular implementation layer (class)
    /// is expected to do/contain *nothing other*
    /// than *clean and direct forwards* of all interface calls
    /// to the lower wrapped service.
    /// By doing these operations within virtuals,
    /// we are then able to create derived classes of this class
    /// which will be able
    /// to selectively override certain interface methods as needed
    /// and within these
    /// call down to the base wrapper's implementation
    /// as/where needed.
    /// But WARNING: do make sure to provide overrides
    /// of *all* of the methods which you would want to do further overriding of
    /// (especially take note of the THREE QueryItems() variants!!!!).
    ///
    /// Please note that interface chaining
    /// best ought to be done in a certain order,
    /// e.g. something like:
    /// [innermost]
    /// 1) most painful (e.g. latency-hampered network access) parts
    /// 2) crucial corrections
    /// 3) data caching
    /// 4) object conversions etc.
    /// [outermost i.e. interface-user facing parts]
    /// (hmm, but OTOH perhaps data caching
    /// should be done *prior* to the correction layer,
    /// since the correction layer might cause large request overhead
    /// which would then go to network in uncached manner?)
    ///
    /// For individual filter classes to be used within an entire filter/processing chain,
    /// please have them implemented in a way
    /// which keeps separate concerns fully separate,
    /// especially for dangerously similar but unrelated filter cases.
    ///
    /// When using this class to implement a filtering component,
    /// you should try to not remove seemingly "superfluous" information
    /// within TFS data,
    /// since these might eventually turn out
    /// to supply much-needed context information
    /// to users of this source control service interface.
    /// IOW, "augment" usefulness/correctness of data supplied by this interface,
    /// rather than "reducing" it.
    /// </remarks>
    public class ITFSSourceControlService_wrapper : TFSSourceControlServiceHelpers, ITFSSourceControlService
    {
        private readonly ITFSSourceControlService scsWrapped = null;

        /// <summary>
        /// Ctor which enables a clean (and thus properly performant)
        /// *ctor-side* class init chaining mechanism
        /// (rather than dirtily assigning it via a post-construction setter).
        /// </summary>
        public ITFSSourceControlService_wrapper(
            ITFSSourceControlService scsWrapped)
        {
            this.scsWrapped = scsWrapped;
        }

        /// <summary>
        /// Provides access to the underlying ITFSSourceControlService interface
        /// which this instance wraps.
        /// </summary>
        /// Performance note:
        /// since this class
        /// is as much of a hotpath
        /// as it can ever get,
        /// I originally had decided
        /// to do direct member accesses
        /// in all wrapper virtuals,
        /// rather than going through properties or so,
        /// *for all locations in this very limited/tiny private layer*.
        /// However, performance overhead of properties
        /// is said to be non-existent:
        /// http://stackoverflow.com/questions/3264833/performance-overhead-for-properties-in-net
        /// , thus definitely prefer using
        /// such abstraction helpers.
        protected ITFSSourceControlService SCSWrapped
        {
            get
            {
                return scsWrapped;
            }
        }

        #region ISourceControlService members
        public virtual void AddWorkspaceMapping(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            string serverPath,
            string localPath,
            int supportedFeatures)
        {
            SCSWrapped.AddWorkspaceMapping(
                tfsUrl,
                credentials,
                workspaceName,
                serverPath,
                localPath,
                supportedFeatures);
        }

        public virtual int Commit(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            string comment,
            IEnumerable<string> serverItems,
            bool deferCheckIn,
            int checkInTicket)
        {
            return SCSWrapped.Commit(
                tfsUrl,
                credentials,
                workspaceName,
                comment,
                serverItems,
                deferCheckIn,
                checkInTicket);
        }

        public virtual void CreateWorkspace(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            string workspaceComment)
        {
            SCSWrapped.CreateWorkspace(
                tfsUrl,
                credentials,
                workspaceName,
                workspaceComment);
        }

        public virtual void DeleteWorkspace(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName)
        {
            SCSWrapped.DeleteWorkspace(
                tfsUrl,
                credentials,
                workspaceName);
        }

        public virtual Guid GetRepositoryId(
            string tfsUrl,
            ICredentials credentials)
        {
            return SCSWrapped.GetRepositoryId(
                tfsUrl,
                credentials);
        }

        public virtual int GetLatestChangeset(
            string tfsUrl,
            ICredentials credentials)
        {
            return SCSWrapped.GetLatestChangeset(
                tfsUrl,
                credentials);
        }

        public virtual WorkspaceInfo[] GetWorkspaces(
            string tfsUrl,
            ICredentials credentials,
            WorkspaceComputers computers,
            int permissionsFilter)
        {
            return SCSWrapped.GetWorkspaces(
                tfsUrl,
                credentials,
                computers,
                permissionsFilter);
        }

        public virtual void PendChanges(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            IEnumerable<PendRequest> requests,
            int pendChangesOptions,
            int supportedFeatures)
        {
            SCSWrapped.PendChanges(
                tfsUrl,
                credentials,
                workspaceName,
                requests,
                pendChangesOptions,
                supportedFeatures);
        }

        public virtual SourceItem[] QueryItems(
            string tfsUrl,
            ICredentials credentials,
            string serverPath,
            RecursionType recursion,
            VersionSpec version,
            DeletedState deletedState,
            ItemType itemType,
            bool sortAscending,
            int options)
        {
            return SCSWrapped.QueryItems(
                tfsUrl,
                credentials,
                serverPath,
                recursion,
                version,
                deletedState,
                itemType,
                sortAscending,
                options);
        }

        public virtual SourceItem[] QueryItems(
            string tfsUrl,
            ICredentials credentials,
            int[] itemIds,
            int changeSet,
            int options)
        {
            return SCSWrapped.QueryItems(
                tfsUrl,
                credentials,
                itemIds,
                changeSet,
                options);
        }

        public virtual LogItem QueryLog(
            string tfsUrl,
            ICredentials credentials,
            string serverPath,
            VersionSpec versionFrom,
            VersionSpec versionTo,
            RecursionType recursionType,
            int maxCount,
            bool sortAscending)
        {
            return SCSWrapped.QueryLog(
                tfsUrl,
                credentials,
                serverPath,
                versionFrom,
                versionTo,
                recursionType,
                maxCount,
                sortAscending);
        }

        public virtual void UndoPendingChanges(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            IEnumerable<string> serverItems)
        {
            SCSWrapped.UndoPendingChanges(
                tfsUrl,
                credentials,
                workspaceName,
                serverItems);
        }

        public virtual void UpdateLocalVersions(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            IEnumerable<LocalUpdate> updates)
        {
            SCSWrapped.UpdateLocalVersions(
                tfsUrl,
                credentials,
                workspaceName,
                updates);
        }

        public virtual void UploadFile(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            string localPath,
            string serverPath)
        {
            SCSWrapped.UploadFile(
                tfsUrl,
                credentials,
                workspaceName,
                localPath,
                serverPath);
        }

        public virtual void UploadFileFromBytes(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            byte[] localContents,
            string serverPath)
        {
            SCSWrapped.UploadFileFromBytes(
                tfsUrl,
                credentials,
                workspaceName,
                localContents,
                serverPath);
        }

        public virtual void UploadFileFromStream(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            Stream localContents,
            string serverPath)
        {
            SCSWrapped.UploadFileFromStream(
                tfsUrl,
                credentials,
                workspaceName,
                localContents,
                serverPath);
        }
        #endregion


        #region ISourceControlService_broken_missing_methods members
        public virtual BranchItem[][] QueryBranches(
            string tfsUrl,
            ICredentials credentials,
            ItemSpec[] items,
            VersionSpec version)
        {
            return SCSWrapped.QueryBranches(
                tfsUrl,
                credentials,
                items,
                version);
        }
        #endregion


        #region ITFSSourceControlService_parts members
        public virtual BranchRelative[][] QueryBranches(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            ItemSpec[] items,
            VersionSpec version)
        {
            return SCSWrapped.QueryBranches(
                tfsUrl,
                credentials,
                workspaceName,
                items,
                version);
        }

        public virtual Changeset[] QueryHistory(
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
            return SCSWrapped.QueryHistory(
                tfsUrl,
                credentials,
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

        public virtual ItemSet[] QueryItems(
            string tfsUrl,
            ICredentials credentials,
            VersionSpec version,
            ItemSpec[] items,
            int options)
        {
            return SCSWrapped.QueryItems(
                tfsUrl,
                credentials,
                version,
                items,
                options);
        }

        public virtual ExtendedItem[][] QueryItemsExtended(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            ItemSpec[] items,
            DeletedState deletedState,
            ItemType itemType,
            int options)
        {
            return SCSWrapped.QueryItemsExtended(
                tfsUrl,
                credentials,
                workspaceName,
                items,
                deletedState,
                itemType,
                options);
        }
        #endregion
    }

    public class TFSSourceControlServiceHelpers
    {
        /// <summary>
        /// Small central comment-enabling helper
        /// for maintaining position indices:
        /// For several interface methods which iterate over TFS data sets,
        /// it's sufficiently relevant during debugging
        /// to know the index of the specific element
        /// that's currently being processed
        /// within a large foreach loop.
        /// </summary>
        /// <remarks>
        /// This central helper could also be used for further useful tasks,
        /// such as e.g. generating debug output
        /// for every modulo result
        /// which indicated that we crossed another 1024 items threshold.
        /// </remarks>
        [Conditional("DEBUG")]
        protected static void DebugMaintainLoopPositionHint(ref int idx)
        {
            ++idx;
        }
    }

    internal class TFSSourceControlService_Statistics_CountRequests_Stats
    {
        public int AddWorkspaceMapping;
        public int Commit;
        public int CreateWorkspace;
        public int DeleteWorkspace;
        public int GetRepositoryId;
        public int GetLatestChangeset;
        public int GetWorkspaces;
        public int PendChanges;
        public int QueryItems_SourceItem_paths;
        public int QueryItems_SourceItem_ids;
        public int QueryLog;
        public int UndoPendingChanges;
        public int UpdateLocalVersions;
        public int UploadFile;
        public int UploadFileFromBytes;
        public int UploadFileFromStream;
        public int QueryBranches;
        public int QueryBranches_workspace;
        public int QueryHistory;
        public int QueryItems_ItemSet;
        public int QueryItemsExtended;
    }

    /// <summary>
    /// Provides interface invocation/call count statistics.
    /// TODO: should also provide a class
    /// which is derived from this one
    /// which collects per-call latency information,
    /// also in averaged form (divided by the number of requests).
    /// </summary>
    public class TFSSourceControlService_Statistics_CountRequests : ITFSSourceControlService_wrapper
    {
        private readonly TFSSourceControlService_Statistics_CountRequests_Stats stats;

        /// <summary>
        /// Debugging helper to be able to quickly determine
        /// which APIs had any activity at all.
        /// </summary>
        [Flags]
        protected enum ReqActivity
        {
            AddWorkspaceMapping = 0x0001,
            Commit = 0x0002,
            CreateWorkspace = 0x0004,
            DeleteWorkspace = 0x0008,
            GetRepositoryId = 0x0010,
            GetLatestChangeset = 0x0020,
            GetWorkspaces = 0x0040,
            PendChanges = 0x0080,
            QueryItems_SourceItem_paths = 0x0100,
            QueryItems_SourceItem_ids = 0x0200,
            QueryLog = 0x0400,
            UndoPendingChanges = 0x0800,
            UpdateLocalVersions = 0x1000,
            UploadFile = 0x2000,
            UploadFileFromBytes = 0x4000,
            UploadFileFromStream = 0x8000,
            QueryBranches = 0x10000,
            QueryBranches_workspace = 0x20000,
            QueryHistory = 0x40000,
            QueryItems_ItemSet = 0x80000,
            QueryItemsExtended = 0x100000
        }
        protected ReqActivity reqActivity; // Debugger use: remember to switch to "Hexadecimal Display" (nope, now that we properly have FlagsAttribute it's not necessary any more)

        public TFSSourceControlService_Statistics_CountRequests(ITFSSourceControlService scsWrapped)
            : base(scsWrapped)
        {
            this.stats = new TFSSourceControlService_Statistics_CountRequests_Stats();
        }

        /// <summary>
        /// Destructor merely provided
        /// to have a convenient breakpoint location
        /// to watch collected statistics in their entirety.
        /// </summary>
        ~TFSSourceControlService_Statistics_CountRequests()
        {
        }

        #region ISourceControlService members
        public override void AddWorkspaceMapping(string tfsUrl,
                                 ICredentials credentials,
                                 string workspaceName,
                                 string serverPath,
                                 string localPath,
                                 int supportedFeatures)
        {
            ++stats.AddWorkspaceMapping;
            reqActivity |= ReqActivity.AddWorkspaceMapping;
            base.AddWorkspaceMapping(tfsUrl, credentials,
                workspaceName,
                serverPath,
                localPath,
                supportedFeatures);
        }

        public override int Commit(string tfsUrl,
                   ICredentials credentials,
                   string workspaceName,
                   string comment,
                   IEnumerable<string> serverItems,
                   bool deferCheckIn,
                   int checkInTicket)
        {
            ++stats.Commit;
            reqActivity |= ReqActivity.Commit;
            return base.Commit(tfsUrl, credentials,
                workspaceName,
                comment,
                serverItems,
                deferCheckIn,
                checkInTicket);
        }

        public override void CreateWorkspace(string tfsUrl,
                             ICredentials credentials,
                             string workspaceName,
                             string workspaceComment)
        {
            ++stats.CreateWorkspace;
            reqActivity |= ReqActivity.CreateWorkspace;
            base.CreateWorkspace(tfsUrl, credentials,
                workspaceName,
                workspaceComment);
        }

        public override void DeleteWorkspace(string tfsUrl,
                             ICredentials credentials,
                             string workspaceName)
        {
            ++stats.DeleteWorkspace;
            reqActivity |= ReqActivity.DeleteWorkspace;
            base.DeleteWorkspace(tfsUrl, credentials,
                workspaceName);
        }


        public override Guid GetRepositoryId(string tfsUrl,
							   ICredentials credentials)
        {
            ++stats.GetRepositoryId;
            reqActivity |= ReqActivity.GetRepositoryId;
            return base.GetRepositoryId(tfsUrl, credentials);
        }


        public override int GetLatestChangeset(string tfsUrl,
                               ICredentials credentials)
        {
            ++stats.GetLatestChangeset;
            reqActivity |= ReqActivity.GetLatestChangeset;
            return base.GetLatestChangeset(tfsUrl, credentials);
        }

        public override WorkspaceInfo[] GetWorkspaces(string tfsUrl,
                                      ICredentials credentials,
                                      WorkspaceComputers computers,
                                      int permissionsFilter)
        {
            ++stats.GetWorkspaces;
            reqActivity |= ReqActivity.GetWorkspaces;
            return base.GetWorkspaces(tfsUrl, credentials,
                computers,
                permissionsFilter);
        }

        public override void PendChanges(string tfsUrl,
                         ICredentials credentials,
                         string workspaceName,
                         IEnumerable<PendRequest> requests,
                         int pendChangesOptions,
                         int supportedFeatures)
        {
            ++stats.PendChanges;
            reqActivity |= ReqActivity.PendChanges;
            base.PendChanges(tfsUrl, credentials,
                workspaceName,
                requests,
                pendChangesOptions,
                supportedFeatures);
        }

        public override SourceItem[] QueryItems(string tfsUrl,
                                ICredentials credentials,
                                string serverPath,
                                RecursionType recursion,
                                VersionSpec version,
                                DeletedState deletedState,
                                ItemType itemType,
                                bool sortAscending,
                                int options)
        {
            ++stats.QueryItems_SourceItem_paths;
            reqActivity |= ReqActivity.QueryItems_SourceItem_paths;
            return base.QueryItems(tfsUrl, credentials,
                serverPath,
                recursion,
                version,
                deletedState,
                itemType,
                sortAscending,
                options);
        }

        public override SourceItem[] QueryItems(string tfsUrl,
                                ICredentials credentials,
                                int[] itemIds,
                                int changeSet,
                                int options)
        {
            ++stats.QueryItems_SourceItem_ids;
            reqActivity |= ReqActivity.QueryItems_SourceItem_ids;
            return base.QueryItems(tfsUrl, credentials,
                itemIds,
                changeSet,
                options);
        }

        public override LogItem QueryLog(string tfsUrl,
                         ICredentials credentials,
                         string serverPath,
                         VersionSpec versionFrom,
                         VersionSpec versionTo,
                         RecursionType recursionType,
                         int maxCount,
                         bool sortAscending)
        {
            ++stats.QueryLog;
            reqActivity |= ReqActivity.QueryLog;
            return base.QueryLog(tfsUrl, credentials,
                serverPath,
                versionFrom,
                versionTo,
                recursionType,
                maxCount,
                sortAscending);
        }

        public override void UndoPendingChanges(string tfsUrl,
                                ICredentials credentials,
                                string workspaceName,
                                IEnumerable<string> serverItems)
        {
            ++stats.UndoPendingChanges;
            reqActivity |= ReqActivity.UndoPendingChanges;
            base.UndoPendingChanges(tfsUrl, credentials,
                workspaceName,
                serverItems);
        }

        public override void UpdateLocalVersions(string tfsUrl,
                                 ICredentials credentials,
                                 string workspaceName,
                                 IEnumerable<LocalUpdate> updates)
        {
            ++stats.UpdateLocalVersions;
            reqActivity |= ReqActivity.UpdateLocalVersions;
            base.UpdateLocalVersions(tfsUrl, credentials,
                workspaceName,
                updates);
        }

        public override void UploadFile(string tfsUrl,
                        ICredentials credentials,
                        string workspaceName,
                        string localPath,
                        string serverPath)
        {
            ++stats.UploadFile;
            reqActivity |= ReqActivity.UploadFile;
            base.UploadFile(tfsUrl, credentials,
                workspaceName,
                localPath,
                serverPath);
        }

        public override void UploadFileFromBytes(string tfsUrl,
                                 ICredentials credentials,
                                 string workspaceName,
                                 byte[] localContents,
                                 string serverPath)
        {
            ++stats.UploadFileFromBytes;
            reqActivity |= ReqActivity.UploadFileFromBytes;
            base.UploadFileFromBytes(tfsUrl, credentials,
                workspaceName,
                localContents,
                serverPath);
        }

        public override void UploadFileFromStream(string tfsUrl,
                                  ICredentials credentials,
                                  string workspaceName,
                                  Stream localContents,
                                  string serverPath)
        {
            ++stats.UploadFileFromStream;
            reqActivity |= ReqActivity.UploadFileFromStream;
            base.UploadFileFromStream(tfsUrl, credentials,
                workspaceName,
                localContents,
                serverPath);
        }
        #endregion


        #region ISourceControlService_broken_missing_methods members
        public override BranchItem[][] QueryBranches(string tfsUrl,
                                     ICredentials credentials,
                                     ItemSpec[] items,
                                     VersionSpec version)
        {
            ++stats.QueryBranches;
            reqActivity |= ReqActivity.QueryBranches;
            return base.QueryBranches(tfsUrl, credentials,
                items,
                version);
        }
        #endregion


        #region ITFSSourceControlService_parts members
        public override BranchRelative[][] QueryBranches(string tfsUrl, ICredentials credentials, string workspaceName, ItemSpec[] items, VersionSpec version)
        {
            ++stats.QueryBranches_workspace;
            reqActivity |= ReqActivity.QueryBranches_workspace;
            return base.QueryBranches(tfsUrl, credentials,
                workspaceName,
                items,
                version);
        }

        public override Changeset[] QueryHistory(string tfsUrl, ICredentials credentials, string workspaceName, string workspaceOwner,
            ItemSpec itemSpec, VersionSpec versionItem, string user, VersionSpec versionFrom, VersionSpec versionTo, int maxCount,
            bool includeFiles, bool generateDownloadUrls, bool slotMode, bool sortAscending
        )
        {
            ++stats.QueryHistory;
            reqActivity |= ReqActivity.QueryHistory;
            return base.QueryHistory(tfsUrl, credentials,
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

        public override ItemSet[] QueryItems(string tfsUrl, ICredentials credentials, VersionSpec version, ItemSpec[] items, int options)
        {
            ++stats.QueryItems_ItemSet;
            reqActivity |= ReqActivity.QueryItems_ItemSet;
            return base.QueryItems(tfsUrl, credentials,
                version,
                items,
                options);
        }

        public override ExtendedItem[][] QueryItemsExtended(string tfsUrl, ICredentials credentials, string workspaceName, ItemSpec[] items, DeletedState deletedState, ItemType itemType, int options)
        {
            ++stats.QueryItemsExtended;
            reqActivity |= ReqActivity.QueryItemsExtended;
            return base.QueryItemsExtended(tfsUrl, credentials,
                workspaceName,
                items,
                deletedState,
                itemType,
                options);
        }
        #endregion
    }

    /// <summary>
    /// Wrapper class which is intended to be
    /// the generic-name outer-layer "typedef"
    /// for *all* TFS bug corrections which one may need to do in total.
    /// Future improvements might include:
    /// - doing certain corrections
    ///   only for those TFS versions where such corrections are needed
    /// </summary>
    /// IMPORTANT order comment:
    /// [[
    /// First (innermost) executing element should be the class
    /// which checks whether a result got delivered
    /// that is properly case-correct vs. its query.
    /// Reason being that this is a simple string match query
    /// where we would already be able
    /// to invalidate the result element
    /// , to then be ignored by subsequent filter elements.
    /// ]]
    /// NOPE!!! It strongly seems as if the first inner handler
    /// ought to be the one which *first* corrects paths,
    /// and the outer handler *then* checks whether the
    /// (now possibly corrected and thus *reliable*, *authentic*) path value
    /// actually also matches the query input.
    /// Not doing it in this order
    /// led to the simple query<->result check to already throw away result items,
    /// and the other handler could then not do its checks properly any more.
    /// The reason that this is problematic most likely is
    /// that the full-path-verifier filter feeds *UNRELIABLE* content
    /// into lower interface parts, yet the other filter expects *input*
    /// to already be *RELIABLE* (which it isn't yet since
    /// we're exactly in the process of
    /// *determining* the reliable value!).
    internal class TFSSourceControlService_BugSanitizerAll : ITFSSourceControlService_wrapper
    {
        public TFSSourceControlService_BugSanitizerAll(
            ITFSSourceControlService scsWrapped)
            : base(
                ConstructWrappedSourceControlService(
                    scsWrapped))
        {
        }

        private static ITFSSourceControlService ConstructWrappedSourceControlService(
            ITFSSourceControlService scsWrapped)
        {
            ITFSSourceControlService scs_wrapper_outermost = scsWrapped;

            bool doSanitize_BugCaseInconsistentCommitRecords = true;
            if (doSanitize_BugCaseInconsistentCommitRecords)
            {
                ITFSSourceControlService scs_wrapper_bug_sanitizer_CaseInconsistentCommitRecords = new TFSSourceControlService_BugSanitizerCaseSensitivityInconsistentCommitRecords(
                    scs_wrapper_outermost);
                scs_wrapper_outermost = scs_wrapper_bug_sanitizer_CaseInconsistentCommitRecords;
            }
            // IMPORTANT NOTE:
            // Order of these filters
            // is more than maximally crucial!!! (see comment above).
            bool doSanitize_BugCaseMismatchingResult = (Configuration.SCMWantCaseSensitiveItemMatch);
            if (doSanitize_BugCaseMismatchingResult)
            {
                ITFSSourceControlService scs_wrapper_bug_sanitizer_CaseMismatchingResult = new TFSSourceControlService_BugSanitizerCaseMismatchingResultVsQuery(scs_wrapper_outermost);
                scs_wrapper_outermost = scs_wrapper_bug_sanitizer_CaseMismatchingResult;
            }

            return scs_wrapper_outermost;
        }
    }

    /// <remarks>
    /// IMPORTANT NOTE: handling within this class
    /// is a rather terrible PERFORMANCE HOTPATH
    /// since it needs to verify tons of paths.
    /// Some of it is useless overhead since certain parts
    /// contain the same path / revision pair even *multiple* times
    /// (especially in the QueryBranches_sanitize() case).
    /// To try to improve the situation,
    /// one should likely introduce a cache class
    /// which provides/keeps mappings
    /// from the potentially-wrong-path / revision pair
    /// to case-verified result path,
    /// and keep this cache as a maximally globally available object,
    /// i.e. for use during at least one filter class session,
    /// or ideally much more (e.g. for all same-credential same-server-url session scopes).
    /// Initially one could introduce the cache object
    /// for within-class-session use only,
    /// and once it works to a satisfying extent,
    /// one could add infrastructure
    /// to be able to share that cache object
    /// within a much larger (yet still compatible) session scope.
    /// </remarks>
    internal class TFSSourceControlService_BugSanitizerCaseSensitivityInconsistentCommitRecords : ITFSSourceControlService_wrapper
    {
        public TFSSourceControlService_BugSanitizerCaseSensitivityInconsistentCommitRecords(
            ITFSSourceControlService scsWrapped)
            : base(
                scsWrapped)
        {
        }

        #region ISourceControlService members
        public override SourceItem[] QueryItems(
            string tfsUrl,
            ICredentials credentials,
            string serverPath,
            RecursionType recursion,
            VersionSpec version,
            DeletedState deletedState,
            ItemType itemType,
            bool sortAscending,
            int options)
        {
            SourceItem[] sourceItems = base.QueryItems(
                tfsUrl,
                credentials,
                serverPath,
                recursion,
                version,
                deletedState,
                itemType,
                sortAscending,
                options);

            MakeBugSanitizer(tfsUrl, credentials).QueryItems_sanitize(ref sourceItems);

            return sourceItems;
        }

        public override SourceItem[] QueryItems(
            string tfsUrl,
            ICredentials credentials,
            int[] itemIds,
            int changeSet,
            int options)
        {
            SourceItem[] sourceItems = base.QueryItems(
                tfsUrl,
                credentials,
                itemIds,
                changeSet,
                options);

            MakeBugSanitizer(tfsUrl, credentials).QueryItems_sanitize(ref sourceItems);

            return sourceItems;
        }

        public override LogItem QueryLog(
            string tfsUrl,
            ICredentials credentials,
            string serverPath,
            VersionSpec versionFrom,
            VersionSpec versionTo,
            RecursionType recursionType,
            int maxCount,
            bool sortAscending)
        {
            LogItem logItem = base.QueryLog(
                tfsUrl,
                credentials,
                serverPath,
                versionFrom,
                versionTo,
                recursionType,
                maxCount,
                sortAscending);

            MakeBugSanitizer(tfsUrl, credentials).QueryLog_sanitize(ref logItem);

            return logItem;
        }
        #endregion



        #region ISourceControlService_broken_missing_methods members
        public override BranchItem[][] QueryBranches(
            string tfsUrl,
            ICredentials credentials,
            ItemSpec[] items,
            VersionSpec version)
        {
            BranchItem[][] branchItems = base.QueryBranches(
                tfsUrl,
                credentials,
                items,
                version);

            MakeBugSanitizer(tfsUrl, credentials).QueryBranches_sanitize(ref branchItems);

            return branchItems;
        }
        #endregion


        #region ITFSSourceControlService_parts members
        public override Changeset[] QueryHistory(
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
            Changeset[] changesets = base.QueryHistory(
                tfsUrl,
                credentials,
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

            MakeBugSanitizer(
                tfsUrl,
                credentials).QueryHistory_sanitize(
                    ref changesets);

            return changesets;
        }

        public override ItemSet[] QueryItems(
            string tfsUrl,
            ICredentials credentials,
            VersionSpec version,
            ItemSpec[] items,
            int options)
        {
            ItemSet[] itemSets = base.QueryItems(
                tfsUrl,
                credentials,
                version,
                items,
                options);

            MakeBugSanitizer(tfsUrl, credentials).QueryItems_sanitize(ref itemSets);

            return itemSets;
        }

        public override ExtendedItem[][] QueryItemsExtended(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            ItemSpec[] items,
            DeletedState deletedState,
            ItemType itemType,
            int options)
        {
            ExtendedItem[][] extendedItems = base.QueryItemsExtended(
                tfsUrl,
                credentials,
                workspaceName,
                items,
                deletedState,
                itemType,
                options);

            MakeBugSanitizer(tfsUrl, credentials).QueryItemsExtended_sanitize(ref extendedItems);

            return extendedItems;
        }
        #endregion


        #region local filtering-related methods
        private TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_SourceControlService MakeBugSanitizer(
            string tfsUrl,
            ICredentials credentials)
        {
            return new TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_SourceControlService(
                base.SCSWrapped,
                tfsUrl, credentials);
        }
        #endregion
    }

    /// <summary>
    /// Helper class to have a new object as required by each new request.
    /// This is needed since subsequent requests
    /// have a specific (potentially different) url/credentials pair.
    /// </summary>
    /// <remarks>
    /// One could conceive a caching mechanism for objects of this class,
    /// but given that the class is very lightweight
    /// that would be overkill.
    /// </remarks>
    internal class TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_SourceControlService : TFSSourceControlServiceHelpers
    {
        private readonly TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder bugSanitizer;

        public TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_SourceControlService(
            ITFSSourceControlService scs,
            string tfsUrl,
            ICredentials credentials)
        {
            this.bugSanitizer = new TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder(
                scs,
                tfsUrl,
                credentials);
        }

        private static VersionSpec GetVersionSpecPrevious(int rev)
        {
            return VersionSpec.FromChangeset(rev - 1);
        }

        private void SourceItem_sanitize(SourceItem sourceItem, bool isFromItem, bool isBranch)
        {
            bool needCheckPath = true;
            if (null == sourceItem)
            {
                needCheckPath = false;
            }

            if (needCheckPath)
            {
                var itemRev = sourceItem.RemoteChangesetId;
                bool needQueryPriorVersion = (isFromItem && !isBranch);
                if (needQueryPriorVersion)
                {
                  --itemRev;
                }
                VersionSpec versionSpecItem = VersionSpec.FromChangeset(itemRev);
                TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Bracketed.EnsureItemPathSanitized(bugSanitizer, ref sourceItem.RemoteName, versionSpecItem, sourceItem.ItemType);
                // possibly id-based lookup of path useful/required??
            }
        }

        public void QueryBranches_sanitize(ref BranchItem[][] branchItemsArrays)
        {
            int dbgBia = 0;
            foreach (var branchItemArray in branchItemsArrays)
            {
                int dbgBi = 0;
                foreach (var branchItem in branchItemArray)
                {
                    bool isRename = TfsLibraryHelpers.IsRenameOperation(branchItem);
                    bool isBranch = (!isRename); // rather than rename - signifies whether .FromItem is still existing at this revision or not!
                    bool needCheckFrom = false;
                    bool needCheckTo = false;
                    needCheckFrom = true;
                    needCheckTo = true;
                    if (needCheckFrom)
                    {
                        SourceItem_sanitize(branchItem.FromItem, true, isBranch);
                    }
                    if (needCheckTo)
                    {
                        SourceItem_sanitize(branchItem.ToItem, false, isBranch);
                    }
                    DebugMaintainLoopPositionHint(ref dbgBi);
                }
                DebugMaintainLoopPositionHint(ref dbgBia);
            }
        }

        // Since I'm somewhat unsure
        // whether/where this might occur (perhaps branch merges?),
        // keep an eye on such cases...
        [Conditional("DEBUG")]
        private static void AssertVersionMatch(
            int version1,
            int version2)
        {
            bool isMatch = (version1 == version2);
            if (!(isMatch))
            {
                Helper.DebugUsefulBreakpointLocation();
                throw new InvalidOperationException(
                    "changeset vs. change version mismatch");
            }
        }

        /// <summary>
        /// Nearly comment-only helper -
        /// central construction helper
        /// which constructs an object which is *not* yet usable
        /// since it is to be assigned its final value *later*
        /// (e.g. in a very tight hotpath loop).
        private static ChangesetVersionSpec ChangesetVersionSpecConstructRawObject()
        {
            return VersionSpec.FromChangeset(-1);
        }

        /// <summary>
        /// Nearly comment-only helper
        /// to be able to efficiently reuse an existing ChangesetVersionSpec
        /// (avoid likely more expensive reconstruction via new).
        /// </summary>
        private static void ChangesetVersionSpecSetVersion(
            ref ChangesetVersionSpec versionSpecChangeset,
            int changesetId)
        {
            versionSpecChangeset.cs = changesetId;
        }

        public void QueryHistory_sanitize(
            ref Changeset[] changesets)
        {
            ChangesetVersionSpec versionSpecChangeset = ChangesetVersionSpecConstructRawObject();
            int dbgCs = 0;
            foreach (var changeset in changesets)
            {
                ChangesetVersionSpecSetVersion(
                    ref versionSpecChangeset,
                    changeset.cset);

                int dbgCg = 0;
                foreach (var change in changeset.Changes)
                {
                    bool needCheckPath = true;
                    if (needCheckPath)
                    {
                        bool isDelete = ((change.type & ChangeType.Delete) == ChangeType.Delete);
                        bool isCurrentVersionUnavailable = (isDelete);
                        bool needQueryPriorVersion = (isCurrentVersionUnavailable);
                        AssertVersionMatch(change.Item.cs, changeset.cset);
                        VersionSpec versionSpecItem = needQueryPriorVersion ? GetVersionSpecPrevious(change.Item.cs) : versionSpecChangeset;
                        try
                        {
                            bugSanitizer.CheckNeedItemPathSanitize(
                                change.Item.item,
                                versionSpecItem,
                                change.Item.type);
                        }
                        catch (ITFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Exception_NeedSanitize e)
                        {
                            change.Item.item = e.PathSanitized;
                        }
                    }
                    DebugMaintainLoopPositionHint(ref dbgCg);
                }
                DebugMaintainLoopPositionHint(ref dbgCs);
            }
        }

        public void QueryItems_sanitize(ref SourceItem[] sourceItems)
        {
            ChangesetVersionSpec versionSpecItem = ChangesetVersionSpecConstructRawObject();
            int dbgSi = 0;
            foreach (var sourceItem in sourceItems)
            {
                bool needCheckChange = true;
                if (needCheckChange)
                {
                    ChangesetVersionSpecSetVersion(
                        ref versionSpecItem,
                        sourceItem.RemoteChangesetId);
                    TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Bracketed.EnsureItemPathSanitized(bugSanitizer, ref sourceItem.RemoteName, versionSpecItem, sourceItem.ItemType);
                }
                DebugMaintainLoopPositionHint(ref dbgSi);
            }
        }

        public void QueryItems_sanitize(ref ItemSet[] itemSets)
        {
            ChangesetVersionSpec versionSpecItem = ChangesetVersionSpecConstructRawObject();
            int dbgSi = 0;
            foreach (var itemSet in itemSets)
            {
                int dbgI = 0;
                foreach (var item in itemSet.Items)
                {
                    bool needCheckChange = true;
                    if (needCheckChange)
                    {
                        ChangesetVersionSpecSetVersion(
                            ref versionSpecItem,
                            item.cs);
                        try
                        {
                            bugSanitizer.CheckNeedItemPathSanitize(item.item, versionSpecItem, item.type);
                        }
                        catch (ITFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Exception_NeedSanitize e)
                        {
                            item.item = e.PathSanitized;
                        }
                    }
                    DebugMaintainLoopPositionHint(ref dbgI);
                }
                DebugMaintainLoopPositionHint(ref dbgSi);
            }
        }

        public void QueryItemsExtended_sanitize(ref ExtendedItem[][] extendedItemsArrays)
        {
            ChangesetVersionSpec versionSpecItem = ChangesetVersionSpecConstructRawObject();
            int dbgEia = 0;
            foreach (var extendedItemArray in extendedItemsArrays)
            {
                int dbgEi = 0;
                foreach (var extendedItem in extendedItemArray)
                {
                    bool needCheckChange = true;
                    if (needCheckChange)
                    {
                        Helper.DebugUsefulBreakpointLocation(); // FIXME: .latest or .lver??
                        ChangesetVersionSpecSetVersion(
                            ref versionSpecItem,
                            extendedItem.latest);
                        Helper.DebugUsefulBreakpointLocation(); // FIXME: .titem or .sitem??
                        // According to MSDN ExtendedItem docs, it seems:
                        // sitem == SourceServerItem  Gets the path to the source server item.
                        // titem == TargetServerItem  Gets the path to the target server item.
                        try
                        {
                            bugSanitizer.CheckNeedItemPathSanitize(extendedItem.titem, versionSpecItem, extendedItem.type);
                        }
                        catch (ITFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Exception_NeedSanitize e)
                        {
                            extendedItem.titem = e.PathSanitized;
                        }
                    }
                    DebugMaintainLoopPositionHint(ref dbgEi);
                }
                DebugMaintainLoopPositionHint(ref dbgEia);
            }
        }

        public void QueryLog_sanitize(ref LogItem logItem)
        {
            SourceItemHistory_sanitize(ref logItem.History);
        }

        private void SourceItemHistory_sanitize(ref SourceItemHistory[] itemHistories)
        {
            ChangesetVersionSpec versionSpecChangeset = ChangesetVersionSpecConstructRawObject();
            int dbgIh = 0;
            foreach (var itemHistory in itemHistories)
            {
                ChangesetVersionSpecSetVersion(
                    ref versionSpecChangeset,
                    itemHistory.ChangeSetID);
                int dbgIc = 0;
                foreach (var change in itemHistory.Changes)
                {
                    bool needCheckChange = true;
                    if (needCheckChange)
                    {
                        bool isDelete = ((change.ChangeType & ChangeType.Delete) == ChangeType.Delete);
                        bool isCurrentVersionUnavailable = (isDelete);
                        bool needQueryPriorVersion = (isCurrentVersionUnavailable);
                        VersionSpec versionSpecItem = needQueryPriorVersion ? GetVersionSpecPrevious(change.Item.RemoteChangesetId) : versionSpecChangeset;
                        TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Bracketed.EnsureItemPathSanitized(bugSanitizer, ref change.Item.RemoteName, versionSpecItem, change.Item.ItemType);
                    }
                    DebugMaintainLoopPositionHint(ref dbgIc);
                }
                DebugMaintainLoopPositionHint(ref dbgIh);
            }
        }
    }

    public sealed class TFSBugSanitizer_CaseMismatchingResultVsQuery_Exception_NeedSanitize : Exception
    {
    }

    /// <summary>
    /// Oh well, *yet another* TFS case insensitivity disease.
    /// The last-ditch assumption here
    /// (which we dearly need to hold on to as the absolute truth)
    /// is that the incoming *client query*
    /// knew exactly which item case it wanted,
    /// i.e. that request data is *correct*.
    /// And if it *is* correct (which we *NEED* to clearly assume),
    /// then we *can* verify
    /// that the result set properly *matches* the query,
    /// and discard content anywhere that this ain't so
    /// (and this correction handling will then contribute to ensuring
    /// that a client will remain able to work with -
    /// and thus supply as new requests - actually *correct* data!).
    /// Example 1:
    /// an incoming query of
    ///     /proj1
    /// where the actually correct (existing) TFS location is
    ///     /Proj1
    /// may have a result of
    ///     /Proj1....
    /// and that *needs* to fail,
    /// since the *incoming query* is incorrect.
    /// Example 2:
    /// an incoming query of
    ///     /Proj2
    /// where the actually correct (existing) TFS location is
    ///     /proj2
    /// (specifically known possible reason:
    /// path has just been case-renamed to /proj2 in this very revision)
    /// may have a result of
    ///     /proj2....
    /// (rather than providing a correct "non-existing item" failure)
    /// and that *needs* to fail,
    /// since the *TFS result* is incorrect.
    /// </summary>
    internal class TFSSourceControlService_BugSanitizerCaseMismatchingResultVsQuery : ITFSSourceControlService_wrapper
    {
        public TFSSourceControlService_BugSanitizerCaseMismatchingResultVsQuery(ITFSSourceControlService scsWrapped)
            : base(scsWrapped)
        {
        }

        public override ItemSet[] QueryItems(string tfsUrl, ICredentials credentials, VersionSpec version, ItemSpec[] items, int options)
        {
            ItemSet[] itemSets = base.QueryItems(tfsUrl, credentials,
                version,
                items,
                options);

            QueryItems_sanitize(ref itemSets);

            return itemSets;
        }

        private static void QueryItems_sanitize(ref ItemSet[] itemSets)
        {
            int dbgIset = 0;
            foreach (var itemSet in itemSets)
            {
                Dictionary<int, bool> itemsToBeRemoved = null;
                string queryPath = itemSet.QueryPath;
                int dbgI = 0;
                foreach (var item in itemSet.Items)
                {
                    try
                    {
                        // Definitely verify exact base path part,
                        // for any sub items which might have been delivered
                        // for a (recursive?) base path query.
                        string itemBasePath = item.item.Substring(0, queryPath.Length);
                        CheckPreciseCaseSensitivityMismatch(
                            itemBasePath,
                            queryPath);
                    }
                    catch(TFSBugSanitizer_CaseMismatchingResultVsQuery_Exception_NeedSanitize)
                    {
                        if (null == itemsToBeRemoved)
                        {
                            itemsToBeRemoved = new Dictionary<int, bool>();
                        }
                        itemsToBeRemoved[item.GetHashCode()] = true;
                    }
                    DebugMaintainLoopPositionHint(ref dbgI);
                }
                bool isSane = (null == itemsToBeRemoved);
                if (!isSane)
                {
                    List<Item> itemsToBeFiltered = new List<Item>(itemSet.Items);
                    itemsToBeFiltered.RemoveAll(elem => (itemsToBeRemoved.ContainsKey(elem.GetHashCode())));
                    itemSet.Items = itemsToBeFiltered.ToArray();
                }
                DebugMaintainLoopPositionHint(ref dbgIset);
            }
        }

        private static void CheckPreciseCaseSensitivityMismatch(string arg1, string arg2)
        {
            bool isMatch = !(Helper.IsStringsPreciseCaseSensitivityMismatch(arg1, arg2));
            if (!(isMatch))
            {
                Helper.DebugUsefulBreakpointLocation();
                throw new TFSBugSanitizer_CaseMismatchingResultVsQuery_Exception_NeedSanitize();
            }
        }
    }

    /// <summary>
    /// "Unusable" (TFS-based) SCM handler class -
    /// it very unfortunately is known
    /// to deliver data streams (commit data) with broken content.
    /// Consequently, this "damage-conveying" class
    /// should always remain an inner/internal/private layer,
    /// i.e. *always* accessible only
    /// when being properly wrapped
    /// by outer damage-correcting layers
    /// and NOT be used directly,
    /// or otherwise at least only
    /// in case it's known
    /// that a particular TFS version
    /// is free from such crippling defects.
    /// For specific details of the various TFS issues
    /// of the data that this interface provides,
    /// see full docs at certain specific inner
    /// data corruption correction helpers
    /// which the outer interface-wrapping correction classes
    /// make use of.
    /// </summary>
	internal class TFSSourceControlService_buggy_database_content : SourceControlService /* concrete foreign class implementing *parts* of the interface */, ITFSSourceControlService
	{
        private readonly DefaultLogger logger;

        public TFSSourceControlService_buggy_database_content(
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

		public virtual ExtendedItem[][] QueryItemsExtended(
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

		public virtual BranchRelative[][] QueryBranches(
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
        // to be an interface-compatible *wrapper*(!)
        // around an internal SourceControlService *member*
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
    /// Honest-to-God interface
    /// which is being offered by any TFSSourceControlService* classes.
    /// Currently consists of both
    /// a TfsLibrary-side ISourceControlService interface
    /// *and* additional methods.
    /// </summary>
    public interface ITFSSourceControlService : ISourceControlService, ISourceControlService_interface_breakage_missing_methods, ITFSSourceControlService_parts
    {
    }

    /// <summary>
    ///  Provides the "interface parts"
    ///  which are provided by the TfsLibrary SourceControlService implementation class *only*
    ///  rather than actually being provided
    ///  by TfsLibrary ISourceControlService interface definition proper already. Ugh.
    /// </summary>
    public interface ISourceControlService_interface_breakage_missing_methods
    {
        /// <summary>
        /// Side note: QueryBranches() API method is VersionControlServer.GetBranchHistory() in newer TFS API.
        /// </summary>
        BranchItem[][] QueryBranches(
            string tfsUrl,
            ICredentials credentials,
            ItemSpec[] items,
            VersionSpec version);
    }

    /// <summary>
    /// Internal (UPDATE: not internal, since that would yield a
    /// "base interface" "is less accessible than interface" error)
    /// component, for the sole purpose
    /// of providing (listing)
    /// local interface extensions/additions
    /// in a cleanly isolated separated manner.
    /// </summary>
    public interface ITFSSourceControlService_parts
    {
        BranchRelative[][] QueryBranches(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            ItemSpec[] items,
            VersionSpec version);

        Changeset[] QueryHistory(
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
            bool sortAscending);

        /// <remarks>
        /// Warning: the ItemSet results actually may have ItemType .Any
        /// (details see TfsLibraryHelpers.IsItemTypeCompatible()).
        /// </remarks>
        ItemSet[] QueryItems(
            string tfsUrl,
            ICredentials credentials,
            VersionSpec version,
            ItemSpec[] items,
            int options);

        ExtendedItem[][] QueryItemsExtended(
            string tfsUrl,
            ICredentials credentials,
            string workspaceName,
            ItemSpec[] items,
            DeletedState deletedState,
            ItemType itemType,
            int options);
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

        /// <summary>
        /// Doh, forgot to (--> do not forget to!) take into account
        /// special "extended compatibility" properties of ItemType.Any...
        /// (.Any may not specifically be
        /// what is expected IOW compared against
        /// e.g. .File, .Folder).
        /// </summary>
        /// <param name="candidate"></param>
        /// <param name="required"></param>
        /// <returns></returns>
        public static bool IsItemTypeCompatible(
            ItemType candidate,
            ItemType required)
        {
            // fully breakpointable syntax here:
            bool isCompatible = (candidate == required);

            if (!isCompatible)
            {
                bool shouldAcceptAnyType = (ItemType.Any == required);
                if (shouldAcceptAnyType)
                {
                    isCompatible = true;
                }
            }

            return isCompatible;
        }

        public static BranchItem ConstructBranchItem(
            SourceItem fromItem,
            SourceItem toItem)
        {
            BranchItem branchItem = new BranchItem { FromItem = fromItem, ToItem = toItem };

            return branchItem;
        }

        /// <summary>
        /// Tries to determine whether a particular BranchItem
        /// was a rename
        /// (.FromItem deleted, .ToItem new location)
        /// rather than a branching
        /// (.FromItem at its changeset taken, then applied as a branch
        /// at .ToItem of current changeset)
        /// operation.
        /// </summary>
        /// XXX: Hmm, perhaps we should also be doing some item id comparison here??
        /// For interesting "detect a rename" details, see also
        ///   https://social.msdn.microsoft.com/Forums/vstudio/en-US/home?searchTerm=How%20to%20merge%20a%20%27Rename%27%20using%20the%20API%20in%20TFS%202010
        /// <param name="branchItem">Branch item to be examined</param>
        /// <returns>true if operation was a rename rather than branch</returns>
        public static bool IsRenameOperation(BranchItem branchItem)
        {
            bool isRename = false;

            bool isValidFromAndToItems = ((null != branchItem.FromItem) && (null != branchItem.ToItem));
            if (isValidFromAndToItems)
            {
                bool isSameRevision = (branchItem.FromItem.RemoteChangesetId == branchItem.ToItem.RemoteChangesetId);
                isRename = (isSameRevision);
            }

            return isRename;
        }
    }
}
