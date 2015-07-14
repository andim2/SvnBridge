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
            bool doSanitize_BugAll = true;
            if (doSanitize_BugAll)
            {
                ITFSSourceControlService scs_wrapper_bug_sanitizer = new TFSSourceControlService_BugSanitizerAll(
                    scs_wrapper_outermost);
                scs_wrapper_outermost = scs_wrapper_bug_sanitizer;
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

    /// <summary>
    /// Wrapper class which is intended to be
    /// the generic-name outer-layer "typedef"
    /// for *all* TFS bug corrections which one may need to do in total.
    /// Future improvements might include:
    /// - doing certain corrections
    ///   only for those TFS versions where such corrections are needed
    /// </summary>
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

        public void QueryHistory_sanitize(
            ref Changeset[] changesets)
        {
            int dbgCs = 0;
            foreach (var changeset in changesets)
            {
                VersionSpec versionSpecChangeset = VersionSpec.FromChangeset(changeset.cset);

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
    }
}
