using System;

namespace CodePlex.TfsLibrary.RepositoryWebSvc
{
    public partial class VersionSpec
    {
        static readonly ChangesetVersionSpec first = FromChangeset(1);
        static readonly LatestVersionSpec latest = new LatestVersionSpec();

        public static ChangesetVersionSpec First
        {
            get { return first; }
        }

        public static LatestVersionSpec Latest
        {
            get { return latest; }
        }

        public static ChangesetVersionSpec FromChangeset(int changesetId)
        {
            ChangesetVersionSpec result = new ChangesetVersionSpec();
            result.cs = changesetId;
            return result;
        }

        public static DateVersionSpec FromDate(DateTime date)
        {
            DateVersionSpec result = new DateVersionSpec();
            result.date = date;
            return result;
        }

        public static LabelVersionSpec FromLabel(string label,
                                                 string scope)
        {
            LabelVersionSpec result = new LabelVersionSpec();
            result.label = label;
            result.scope = scope;
            return result;
        }

        public static WorkspaceVersionSpec FromWorkspace(string name,
                                                         string owner)
        {
            WorkspaceVersionSpec result = new WorkspaceVersionSpec();
            result.name = name;
            result.owner = owner;
            return result;
        }
    }
}