namespace SvnBridge.SourceControl
{
    using System; // StringSplitOptions
    using System.Collections.Generic; // List
    using System.Diagnostics; // Conditional
    using System.Net; // ICredentials
    using System.Text; // StringBuilder
    using CodePlex.TfsLibrary; // NetworkAccessDeniedException
    using CodePlex.TfsLibrary.ObjectModel; // SourceItem
    using CodePlex.TfsLibrary.RepositoryWebSvc; // DeletedState, ItemType, VersionSpec
    using Cache; // WebCache
    using Infrastructure; // Container
    using Interfaces; // ITFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder
    using Utility; // Helper.DebugUsefulBreakpointLocation()

    /// <summary>
    /// For a description of the things
    /// that we intend to fix here,
    /// please see docs at its interface.
    /// </summary>
    public class TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder : ITFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder
    {
        private const char path_separator_c = '/';
        private const string path_separator_s = "/";
        private static char[] pathElemSeparators = new char[] { path_separator_c };

        private readonly ITFSSourceControlService sourceControlService;
        private readonly string serverUrl;
        private readonly ICredentials credentials;
        // We don't need <see cref="MemoryBasedPersistentCache"/> type
        // since in our case I believe
        // we *can* (i.e. it's not a critical error) and *should*
        // live with the restriction of <see cref="WebCache"/> type
        // of values getting expired at any time.
        private readonly WebCache cache;
        private readonly string cacheScopePrefix;

        public TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder(
            ITFSSourceControlService sourceControlService,
            string serverUrl, ICredentials credentials)
        {
            this.sourceControlService = sourceControlService;
            this.serverUrl = serverUrl;
            this.credentials = credentials;
            this.cache = GetGloballySharedInstanceOfWebCache();
            string scopeType = GetType().Name;
            string scopeAuthSessionSpecific = GetServerAuthSessionCacheKey(
                serverUrl,
                credentials);
            this.cacheScopePrefix = scopeType + "_" + scopeAuthSessionSpecific;
        }

        /// <summary>
        /// Gets the global <see cref="WebCache"/> instance
        /// where results created by *any* objects of this class are stored.
        /// </summary>
        private WebCache GetGloballySharedInstanceOfWebCache()
        {
            return Container.Resolve<WebCache>();
        }

        // XXX move to more public location!
        private static string GetServerAuthSessionCacheKey(
            string serverUrl,
            ICredentials credentials)
        {
            return serverUrl + GetCredentialsHashCode(
                credentials);
        }

        /// <summary>
        /// Returns a string representation of *hash code specifics*
        /// (IOW, either numeric or "(null)").
        /// Credentials might have ended up null
        /// in cases such as
        /// HttpWebRequest..UnsafeAuthenticatedConnectionSharing = true;
        /// </summary>
        private static string GetCredentialsHashCode(
            ICredentials credentials)
        {
            return (null != credentials) ? credentials.GetHashCode().ToString() : "(null)";
        }

        private static string[] PathSplit(
            string path)
        {
            string[] pathElems = path.Split(pathElemSeparators, StringSplitOptions.RemoveEmptyEntries);
            return pathElems;
        }

        private static string PathJoin(
            string[] pathElems,
            int count)
        {
            return string.Join(
                path_separator_s,
                pathElems,
                0,
                count);
        }

        /// <summary>
        /// Centrally comment-enabling helper.
        /// Arrives at a decision
        /// by taking into account all related parameters.
        /// </summary>
        /// <returns></returns>
        bool ShouldIgnoreQueryItemsException_NetworkAccessDeniedException(
            NetworkAccessDeniedException e,
            string path,
            VersionSpec versionSpec,
            ItemType itemType)
        {
            bool ignore = false;
            // Known cases where this exception may be thrown:
            // - legitimate NetworkAccessDeniedException
            // - signalling unavailability of an item
            //   at that particular location/revision:
            //   - it may have been deleted (ChangeType.Delete)
            //     at that very revision:
            //     $/SomeTeamProj/SomeProj/SomeItemDeletedNow.txt
            //   - it may have been a previously deleted item
            //     whose parent directory has been moved
            //     to a different location,
            //     in which case TFS indicates a ChangeType.None
            //     for such previously-deleted items
            //     (most likely it is some kind of important hinting marker
            //     e.g. for the case where multiple branches
            //     may get merged and some of those do/don't contain
            //     that file any more):
            //     $/SomeTeamProj/SomeProj/SomeItemDeletedPreviously.txt
            //     -->
            //     $/SomeTeamProj/SomeProjRenamed/SomeItemDeletedPreviously.txt
            // We need to ignore *certain* cases of these exceptions:

            ignore = true;

            return ignore;
        }

        public virtual void CheckNeedItemPathSanitize(
            string pathToBeChecked,
            VersionSpec versionSpec,
            ItemType itemType)
        {
            string cacheKey = GetCacheKey(
                pathToBeChecked,
                versionSpec,
                itemType);
            // Note: in case of cache-existing mismatch record,
            // method will simply directly "re-play" exception.
            bool haveCacheEntry = RecallCacheEntry(
                cacheKey,
                pathToBeChecked);
            bool needActiveQuery = !(haveCacheEntry);
            if (needActiveQuery)
            {
                // Prefer to keep this sub scope directly visible here (inlined)
                // due to too many symmetry restrictions:
                // - symmetry RecallCacheEntry() vs. AddCacheEntry()
                // - (related:) symmetry of cacheKey use
                string pathSanitized = null;
                try
                {
                    CheckNeedItemPathSanitize_execute(
                        pathToBeChecked,
                        versionSpec,
                        itemType);
                    pathSanitized = pathToBeChecked;
                }
                catch(ITFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Exception_NeedSanitize e)
                {
                    pathSanitized = e.PathSanitized;
                    throw;
                }
                finally
                {
                    bool haveResult = (null != pathSanitized);
                    if (haveResult)
                    {
                        AddCacheEntry(
                            cacheKey,
                            pathSanitized);
                    }
                }
            }
        }

        private string GetCacheKey(
            string pathToBeChecked,
            VersionSpec versionSpec,
            ItemType itemType)
        {
            // Since SCM lookup supports several different kinds of "version specification",
            // we will somehow have to deal with decoding/labeling them properly, *here*.
            string versionSpecDescription = TfsLibraryHelpers.GetVersionSpecDescription(
                versionSpec);
            return GetCacheKeyConcat(
                this.cacheScopePrefix,
                pathToBeChecked,
                versionSpecDescription,
                itemType.ToString());
        }

        private static string GetCacheKeyConcat(
            string cacheScopePrefix,
            string pathToBeChecked,
            string versionSpecDescription,
            string itemTypeString)
        {
            string cacheKey =
                cacheScopePrefix +
                "_" +
                "path:" + pathToBeChecked +
                "_" +
                versionSpecDescription +
                "_" +
                "type:" + itemTypeString;
            return cacheKey;
        }

        /// <remarks>
        /// StringBuilder is said to be starting to get useful from ~ > 600 strings only!
        /// </remarks>
        private static string GetCacheKeySB(
            string cacheScopePrefix,
            string pathToBeChecked,
            string versionSpecDescription,
            string itemTypeString)
        {
            var sb = new StringBuilder();
            sb.Append(cacheScopePrefix);
            sb.Append("_");
            sb.Append("path:");
            sb.Append(pathToBeChecked);
            sb.Append("_");
            sb.Append(versionSpecDescription);
            sb.Append("_");
            sb.Append("type:");
            sb.Append(itemTypeString);
            string res = sb.ToString(); // debug helper
            return res;
        }

        private bool RecallCacheEntry(
            string cacheKey,
            string pathToBeChecked)
        {
            bool haveCacheEntry = false;

            string pathResult;
            haveCacheEntry = (GetCacheEntry(cacheKey, out pathResult));
            if (haveCacheEntry)
            {
                CheckReportMismatch(
                    pathToBeChecked,
                    pathResult);
            }

            return haveCacheEntry;
        }

        private static void CheckReportMismatch(
            string pathToBeChecked,
            string pathCorrected)
        {
            bool isMatch = pathToBeChecked.Equals(pathCorrected);
            if (!(isMatch))
            {
                ReportSanitizedPath(
                    pathCorrected);
            }
        }

        private bool GetCacheEntry(
            string cacheKey,
            out string pathResult)
        {
            bool haveCacheEntry = false;

            CachedResult result = cache.Get(cacheKey);
            if (null != result)
            {
                pathResult = (string)result.Value;
                haveCacheEntry = true;
            }
            else
            {
                pathResult = "";
            }

            return haveCacheEntry;
        }

        private void AddCacheEntry(
            string cacheKey,
            string pathSanitized)
        {
            // Side note: unfortunately the amount of cache additions here adds up,
            // resulting in (semi-)persistent memory use increases
            // of sometimes ~ 100MB during only one prolonged client session,
            // which may be a bit much
            // since excessive cases
            // might manage to exceed system resources.
            // To keep resource use down reliably,
            // cache size limiting
            // (via Clear()ing once having exceeded a limit)
            // is needed somewhere in central areas.
            cache.Set(cacheKey, pathSanitized);
        }

        private void CheckNeedItemPathSanitize_execute(
            string pathToBeChecked,
            VersionSpec versionSpec,
            ItemType itemType)
        {
            EnsureServerRootSyntax(pathToBeChecked);

            bool wantCheckItem = false; // debug tweak helper var
            if (wantCheckItem)
            {
                CheckItem(pathToBeChecked, 0, itemType);
            }

            string[] pathElemsOrig = PathSplit(pathToBeChecked);
            int pathElemsCount = pathElemsOrig.Length;
            bool isValidPath = (0 < pathElemsCount);
            if (!(isValidPath))
            {
                throw new InvalidPathException(pathToBeChecked);
            }

            //DoCheckNeedItemPathSanitize_slow_single_queries(pathElemsOrig, versionSpec, itemType);
            try
            {
                DoCheckNeedItemPathSanitize_batched(pathElemsOrig, versionSpec, itemType);
            }
            // Definitely need to have our specific notification exception type
            // passed on unfiltered...
            catch (ITFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Exception_NeedSanitize)
            {
                throw;
            }
            catch
            {
                // Hmm, batched mode failed
                // (e.g. a NetworkAccessDeniedException may occur) -
                // let's give single-query mode a chance to succeed...
                // (even if only partially
                // for *some* sub items in the path hierarchy!)
                DoCheckNeedItemPathSanitize_slow_single_queries(pathElemsOrig, versionSpec, itemType);
            }
        }

        private static ItemSpec[] PathHierarchyToItemSpecs(string[] pathElems)
        {
            var pathElemsCount = pathElems.Length;

            List<ItemSpec> itemSpecs = new List<ItemSpec>(pathElemsCount - 1);
            for (var numElemsRemain = pathElemsCount; numElemsRemain > 1; --numElemsRemain)
            {
                string path = PathJoin(pathElems, numElemsRemain);
                ItemSpec itemSpec = new ItemSpec { item = path, recurse = RecursionType.None };
                itemSpecs.Add(itemSpec);
            }

            return itemSpecs.ToArray();
        }

        private static void SanitizePathElemsViaItemSet(string[] pathElemsToBeChecked, ItemType itemType, ItemSet[] itemSetsHierarchical)
        {
            bool haveEncounteredAnyMismatch = false;

            int pathElemsCount = pathElemsToBeChecked.Length;

            int numElemsRemain = pathElemsCount;
            foreach (var itemSet in itemSetsHierarchical)
            {
                bool isRoot = (1 == numElemsRemain);
                if (isRoot)
                {
                    break;
                }

                string pathToBeChecked = PathJoin(pathElemsToBeChecked, numElemsRemain);

                foreach (var item in itemSet.Items)
                {
                    bool isItemTypeCompatible = TfsLibraryHelpers.IsItemTypeCompatible(
                        item.type,
                        itemType);
                    if (isItemTypeCompatible)
                    {
                        string pathResult = item.item;
                        bool isPathMatch = pathResult.Equals(pathToBeChecked);
                        if (!(isPathMatch))
                        {
                            haveEncounteredAnyMismatch = true;
                            var idxPathElemToBeCorrected = numElemsRemain - 1;
                            HandlePathElemMismatch(ref pathElemsToBeChecked, idxPathElemToBeCorrected, pathResult);
                        }
                    }
                }
                --numElemsRemain;
                itemType = ItemType.Folder; // any item other than FQPN *must* be a parent folder
            }

            bool hadSanePath = !(haveEncounteredAnyMismatch);
            if (!(hadSanePath))
            {
                ReportSanitizedPathFromElems(pathElemsToBeChecked);
            }
        }

        private void DoCheckNeedItemPathSanitize_batched(string[] pathElemsOrig,
                                                                VersionSpec versionSpec,
                                                                ItemType itemType)
        {
            string[] pathElemsSanitized = pathElemsOrig;

            ItemSet[] itemSets;
            {
                ItemSpec[] itemSpecs = PathHierarchyToItemSpecs(pathElemsOrig);

                itemSets = sourceControlService.QueryItems(serverUrl, credentials,
                    versionSpec,
                    itemSpecs,
                    0);
            }

            SanitizePathElemsViaItemSet(pathElemsSanitized, itemType, itemSets);
        }

        private void DoCheckNeedItemPathSanitize_slow_single_queries(string[] pathElemsOrig,
                                                                     VersionSpec versionSpec,
                                                                     ItemType itemType)
        {
            bool haveEncounteredAnyMismatch = false;

            int pathElemsCount = pathElemsOrig.Length;

            string[] pathElemsToBeChecked = pathElemsOrig;
            string[] pathElemsSanitized = pathElemsOrig;

            // I believe we do need to carry out queries
            // in order of outermost item *down to* root path
            // rather than the other way around,
            // right?
            // If so, please document all important reasons.
            // Or perhaps we could experiment with providing
            // alternative implementations of this interface
            // which do things slightly differently
            // and thus with differing performance characteristics...
            //
            // And I guess we prefer working via path elem arrays
            // rather than reducing string length
            // from the last separator each time
            // (that probably would be more imprecise/risky).
            ItemType itemTypeCurr = itemType;
            // SPECIAL NOTE: I decided to start at pathElemsCount - 1
            // rather than the full item path,
            // since querying the item itself is not interesting,
            // and we need to try hard to achieve maximum performance
            // of this *EXTREMELY PAINFUL* correction overhead
            // (3 or 4 network queries for each path!!).
            // And so far implementing a sufficiently generic/global SCS cache layer
            // remains difficult...
            //
            // Also, we'll skip last iteration (by doing "numElemsRemain > 1"),
            // since that element is the TFS root ($/),
            // and when doing a root-only request, TFS APIs would bail out...
            for (var numElemsRemain = pathElemsCount - 1; numElemsRemain > 1; --numElemsRemain)
            {
                bool isLastPathElem = (pathElemsCount == numElemsRemain);
                bool isFolder = (!isLastPathElem);
                if (isFolder)
                {
                    itemTypeCurr = ItemType.Folder;
                }

                string pathToBeChecked_Curr = PathJoin(pathElemsToBeChecked, numElemsRemain);
                try
                {
                    SourceItem sourceItem = QueryItem(
                        pathToBeChecked_Curr,
                        versionSpec,
                        itemTypeCurr);
                    string pathResult = sourceItem.RemoteName;
                    bool isPathElemMatch = pathResult.Equals(pathToBeChecked_Curr);
                    if (!(isPathElemMatch))
                    {
                        haveEncounteredAnyMismatch = true;
                        var idxPathElemToBeCorrected = numElemsRemain - 1;
                        HandlePathElemMismatch(
                            ref pathElemsSanitized,
                            idxPathElemToBeCorrected,
                            pathResult);
                    }
                    // UPDATE: NOPE, we now use a count-based .Join():
                    //// Hmm, I guess decreasing like this
                    //// is more convenient
                    //// than keeping things in a list
                    //// and then having to do .ToArray() each time...
                    //Array.Resize<string>(ref pathElemsToBeChecked, pathElemsToBeChecked.Length - 1);
                }
                catch (NetworkAccessDeniedException e)
                {
                    Helper.DebugUsefulBreakpointLocation();
                    bool shouldIgnoreException = ShouldIgnoreQueryItemsException_NetworkAccessDeniedException(
                        e,
                        pathToBeChecked_Curr,
                        versionSpec,
                        itemTypeCurr);
                    if (shouldIgnoreException)
                    {
                        // Nothing extra to be done here -
                        // since we had an exception, query of the current item hierarchy failed,
                        // thus we resort to simply skipping to the next one.
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            bool hadSanePath = !(haveEncounteredAnyMismatch);
            if (!(hadSanePath))
            {
                ReportSanitizedPathFromElems(
                    pathElemsSanitized);
            }
        }

        private SourceItem QueryItem(
            string pathToBeChecked,
            VersionSpec versionSpec,
            ItemType itemType)
        {
            SourceItem[] sourceItems = sourceControlService.QueryItems(serverUrl, credentials,
                pathToBeChecked,
                RecursionType.None,
                versionSpec,
                DeletedState.Any, // This is ok, right?
                itemType,
                false, 0);
            bool isCorrectResultItemCount = (1 == sourceItems.Length);
            if (!(isCorrectResultItemCount))
            {
                throw new NotExactlyOneResultItemException();
            }

            return sourceItems[0];
        }

        private static void EnsureServerRootSyntax(string path)
        {
            string serverRootPath = SvnBridge.Constants.ServerRootPath;
            bool isServerRootSyntax = path.StartsWith(serverRootPath);
            bool haveExpectedFullServerRootSyntax = (isServerRootSyntax);
            if (!(haveExpectedFullServerRootSyntax))
            {
                throw new UnexpectedPathFormatException(path, serverRootPath);
            }
        }

        public sealed class UnexpectedPathFormatException : ArgumentException
        {
            public UnexpectedPathFormatException(string path, string serverRootPath)
                : base("Unexpected format of path " + path + ": does not start with server root syntax part " + serverRootPath)
            {
                Helper.DebugUsefulBreakpointLocation();
            }
        }

        public sealed class InvalidPathException : ArgumentException
        {
            public InvalidPathException(string path)
                : base(string.Format("empty/incompatible path \"{0}\"", path))
            {
                Helper.DebugUsefulBreakpointLocation();
            }
        }

        /// <summary>
        /// Hopefully useful debug helper.
        /// </summary>
        [Conditional("DEBUG")]
        private void CheckItem(string pathToBeChecked, int rev, ItemType itemType)
        {
            BranchItem[][] branches;
            Changeset[] history;
            SourceItem[] sourceItems;

            VersionSpec versionSpec = VersionSpec.FromChangeset(rev);
            ItemSpec itemSpec = new ItemSpec { item = pathToBeChecked, recurse = RecursionType.None };
            ItemSpec[] itemSpecs = new ItemSpec[] { itemSpec };
            try
            {
                bool querybranches = true;
                bool queryhistory = true;
                bool queryitem = true;

                if (querybranches)
                {
                    branches = sourceControlService.QueryBranches(serverUrl, credentials, itemSpecs, versionSpec);
                }
                if (queryhistory)
                {
                    history = sourceControlService.QueryHistory(serverUrl, credentials,
                        null,
                        null,
                        itemSpec,
                        versionSpec,
                        null,
                        versionSpec,
                        versionSpec,
                        1,
                        true,
                        false,
                        false,
                        false);

                }
                if (queryitem)
                {
                     sourceItems = sourceControlService.QueryItems(serverUrl, credentials,
                        pathToBeChecked,
                        RecursionType.None,
                        versionSpec,
                        DeletedState.Any, // This is ok, right?
                        itemType,
                        false, 0
                    );
                }
            }
            catch
            {
            }
        }

        public sealed class NotExactlyOneResultItemException : InvalidOperationException
        {
            public NotExactlyOneResultItemException()
                : base("not exactly one result item found")
            {
                Helper.DebugUsefulBreakpointLocation();
            }
        }

        private static void HandlePathElemMismatch(
            ref string[] pathElemsToBeCorrected,
            int idxPathElemToBeCorrected,
            string pathCorrected)
        {
            Helper.DebugUsefulBreakpointLocation();
            string[] pathElemsCorrected = PathSplit(pathCorrected);

            // Now tweak/bend/correct
            // exactly the *single* path element
            // that has astonishingly been determined
            // to be incorrect at this time:
            pathElemsToBeCorrected[idxPathElemToBeCorrected] = pathElemsCorrected[idxPathElemToBeCorrected];
        }

        private static void ReportSanitizedPathFromElems(
            string[] pathElemsSanitized)
        {
            string pathSanitized = PathJoin(
                pathElemsSanitized,
                pathElemsSanitized.Length);
            ReportSanitizedPath(
                pathSanitized);
        }

        private static void ReportSanitizedPath(
            string pathSanitized)
        {
            throw new ITFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Exception_NeedSanitize(
                pathSanitized);
        }
    }

    public class TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Bracketed
    {
        /// <summary>
        /// Simply bracketing/scoping/instruction-cache-optimizing helper.
        /// Does everything that's usually needed internally,
        /// and as an added bonus
        /// the particular candidate parameter to be modified
        /// has to be referenced only once, too.
        /// Not usable in certain cases where the reference cannot be passed directly, though:
        /// "error CS0206: A property, indexer or dynamic member access may not be passed as an out or ref parameter"
        /// (should be using an explicit try /catch frame then (to keep exceptional handling in exception path).
        /// </summary>
        public static void EnsureItemPathSanitized(
            TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder bugSanitizer,
            ref string itemPathToBeSanitized,
            VersionSpec versionSpecItem,
            ItemType itemType)
        {
            try
            {
                bugSanitizer.CheckNeedItemPathSanitize(
                    itemPathToBeSanitized,
                    versionSpecItem,
                    itemType);
            }
            catch (ITFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Exception_NeedSanitize e)
            {
                itemPathToBeSanitized = e.PathSanitized;
            }
        }

        public static bool CheckNeededItemPathSanitized(
            TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder bugSanitizer,
            ref string itemPathToBeSanitized,
            VersionSpec versionSpecItem,
            ItemType itemType)
        {
            bool hadSanePath = true;
            try
            {
                bugSanitizer.CheckNeedItemPathSanitize(
                    itemPathToBeSanitized,
                    versionSpecItem,
                    itemType);
            }
            catch (ITFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder_Exception_NeedSanitize e)
            {
                itemPathToBeSanitized = e.PathSanitized;
                hadSanePath = false;
            }

            return !(hadSanePath);
        }
    }
}
