namespace CodePlex.TfsLibrary
{
    public interface IAppConfig
    {
        string AnonymousUsername { get; }

        bool AttemptAutoMerge { get; }

        string CodePlexWebServiceUrl { get; }

        bool DefaultToGuiForCommit { get; }

        bool DefaultToGuiForStatus { get; }

        string DiffArgs { get; }

        string DiffTool { get; }

        string Editor { get; }

        bool ForceBasicAuth { get; }

        string IgnoreFile { get; }

        string MergeArgs { get; }

        string MergeTool { get; }

        bool TryDefaultCredentials { get; }
    }
}