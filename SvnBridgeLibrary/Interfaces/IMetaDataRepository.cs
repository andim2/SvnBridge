using System; // StringComparison
using CodePlex.TfsLibrary.ObjectModel;
using SvnBridge.SourceControl;

// XXX: using:s for MetaDataRepositoryBase only (see below)
using System.Net; // ICredentials
using SvnBridge.Interfaces; // IMetaDataRepository
using SvnBridge.Proxies; // TracingInterceptor

namespace SvnBridge.Interfaces
{
    public interface IMetaDataRepository
    {
        SourceItem[] QueryItems(int revision, int itemId);
        SourceItem[] QueryItems(int revision, string path, Recursion recursion);
        SourceItem[] QueryItems(int revision, string[] paths, Recursion recursion);
    }
}

// XXX: move this class into its own new Infrastructure/MetaDataRepositoryBase.cs file!

namespace SvnBridge.Infrastructure
{
    [Interceptor(typeof(TracingInterceptor))]
    public abstract class MetaDataRepositoryBase : IMetaDataRepository
    {
        protected readonly TFSSourceControlService sourceControlService;
        protected readonly string serverUrl;
        protected readonly ICredentials credentials;
        protected readonly string rootPath;
        protected readonly bool wantCaseSensitiveMatch;

        protected MetaDataRepositoryBase(
            TFSSourceControlService sourceControlService,
            string serverUrl,
            ICredentials credentials,
            string rootPath)
        {
            this.sourceControlService = sourceControlService;
            this.serverUrl = serverUrl;
            this.credentials = credentials;
            this.rootPath = rootPath;
            this.wantCaseSensitiveMatch = Configuration.SCMWantCaseSensitiveItemMatch;
        }

        protected string GetServerPath(string path)
        {
            if (path.StartsWith("$//"))
                return Constants.ServerRootPath + path.Substring(3);

            if (path.StartsWith("$/"))
                return path;

            string serverPath = rootPath;

            if (serverPath.EndsWith("/"))
                serverPath = serverPath.Substring(0, serverPath.Length - 1);

            serverPath += (path.StartsWith("/") == false) ?
                '/' + path
              :
                path
            ;

            if (serverPath.EndsWith("/") && serverPath != "$/")
                serverPath = serverPath.Substring(0, serverPath.Length - 1);

            return serverPath;
        }

        public abstract SourceItem[] QueryItems(int revision, int itemId);
        public abstract SourceItem[] QueryItems(int revision, string path, Recursion recursion);
        public abstract SourceItem[] QueryItems(int revision, string[] paths, Recursion recursion);

        protected SourceItem[] Service_QueryItems(
            int revision,
            int itemId)
        {
            return sourceControlService.QueryItems(serverUrl, credentials,
                new int[] { itemId },
                revision,
                0);
        }
    }
}
