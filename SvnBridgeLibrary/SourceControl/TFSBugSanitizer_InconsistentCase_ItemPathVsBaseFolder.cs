namespace SvnBridge.SourceControl
{
    using System; // StringSplitOptions
    using System.Net; // ICredentials
    using CodePlex.TfsLibrary.ObjectModel; // SourceItem
    using CodePlex.TfsLibrary.RepositoryWebSvc; // DeletedState, ItemType, VersionSpec
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

        public TFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder(
            ITFSSourceControlService sourceControlService,
            string serverUrl, ICredentials credentials)
        {
            this.sourceControlService = sourceControlService;
            this.serverUrl = serverUrl;
            this.credentials = credentials;
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

        public virtual void CheckNeedItemPathSanitize(
            string pathToBeChecked,
            VersionSpec versionSpec,
            ItemType itemType)
        {
            CheckNeedItemPathSanitize_execute(
                pathToBeChecked,
                versionSpec,
                itemType);
        }

        private void CheckNeedItemPathSanitize_execute(
            string pathToBeChecked,
            VersionSpec versionSpec,
            ItemType itemType)
        {
            bool haveEncounteredAnyMismatch = false;

            EnsureServerRootSyntax(pathToBeChecked);

            string[] pathElemsOrig = PathSplit(pathToBeChecked);
            int pathElemsCount = pathElemsOrig.Length;
            bool isValidPath = (0 < pathElemsCount);
            if (!(isValidPath))
            {
                throw new InvalidPathException(pathToBeChecked);
            }

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
            for (var numElemsRemain = pathElemsCount; numElemsRemain > 0; --numElemsRemain)
            {
                bool haveHitRoot = (1 == numElemsRemain);
                if (haveHitRoot)
                {
                    // When doing a root-only request, TFS APIs would bail out...
                    break;
                }
                bool isLastPathElem = (pathElemsCount == numElemsRemain);
                bool isFolder = (!isLastPathElem);
                if (isFolder)
                {
                    itemTypeCurr = ItemType.Folder;
                }

                string pathToBeChecked_Curr = PathJoin(
                    pathElemsToBeChecked,
                    numElemsRemain);
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
