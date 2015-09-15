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
            string[] pathElems)
        {
            return string.Join(
                path_separator_s,
                pathElems);
        }

        public virtual string GetItemPathSanitized(
            string pathToBeChecked,
            int revision)
        {
            string pathSanitized;
            bool haveEncounteredAnyMismatch = false;

            // FIXME: should likely have VersionSpec directly at interface layer...
            VersionSpec versionSpec = VersionSpec.FromChangeset(revision);

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
            for (var numElemsRemain = pathElemsCount; numElemsRemain > 0; --numElemsRemain)
            {
                string pathToBeChecked_Curr = PathJoin(
                    pathElemsToBeChecked);
                SourceItem sourceItem = QueryItem(
                    pathToBeChecked_Curr,
                    versionSpec);
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
                // Hmm, I guess decreasing like this
                // is more convenient
                // than keeping things in a list
                // and then having to do .ToArray() each time...
                Array.Resize<string>(ref pathElemsToBeChecked, pathElemsToBeChecked.Length - 1);
            }

            bool hadSanePath = !(haveEncounteredAnyMismatch);
            if (!(hadSanePath))
            {
                pathSanitized = PathJoin(
                    pathElemsSanitized);
            }
            else
            {
                pathSanitized = pathToBeChecked;
            }

            return pathSanitized;
        }

        private SourceItem QueryItem(
            string pathToBeChecked,
            VersionSpec versionSpec)
        {
            SourceItem[] sourceItems = sourceControlService.QueryItems(serverUrl, credentials,
                pathToBeChecked,
                RecursionType.None,
                versionSpec,
                DeletedState.Any, // This is ok, right?
                ItemType.Any, // FIXME: really!?!? perhaps we could have a case of same-name .File and .Folder, where we would in fact do need to make this distinction!!!
                false, 0);
            bool isCorrectResultItemCount = (1 == sourceItems.Length);
            if (!(isCorrectResultItemCount))
            {
                throw new NotExactlyOneResultItemException();
            }

            return sourceItems[0];
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
    }
}
