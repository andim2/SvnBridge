using System;
using SvnBridge.SourceControl;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;

namespace SvnBridge.Infrastructure
{
    public class FileRepository
    {
        private readonly ICredentials credentials;
        private readonly IWebTransferService webTransferService;

        public FileRepository(string serverUrl, ICredentials credentials, IWebTransferService webTransferService)
        {
            this.credentials = CredentialsHelper.GetCredentialsForServer(serverUrl, credentials);
            this.webTransferService = webTransferService;
        }

        public virtual IAsyncResult BeginReadFile(
            string fileUrl,
            Guid repositoryUuid,
            AsyncCallback callback)
        {
            return webTransferService.BeginDownloadBytes(
                GetDownloadUrl(
                    fileUrl,
                    repositoryUuid),
                credentials,
                callback);
        }

        public virtual byte[] EndReadFile(
            IAsyncResult ar)
        {
            return webTransferService.EndDownloadBytes(
                ar);
        }

        /// <summary>
        /// OUTDATED (non-asynchronous i.e. blocking,
        /// and strange foreign-type param dependency) API variant, DO NOT USE.
        /// </summary>
        public virtual byte[] GetFile(ItemMetaData item, Guid repositoryUuid)
        {
            // FIXME: I'm not completely happy with the layering here -
            // A file repository should provide a simple mapping of "identifier" to "result object".
            // It should NOT be concerned with being fed with a repository ID
            // since I'd assume that one to always be identical
            // for all uses of the repository *within one session*
            // (and let's not forget that this handling here got bloated beyond recognition
            // for the simple if special case of TFS proxy server support only).
            // So the objective would probably be to provide a class which contains *all* necessary info builtin,
            // where an object of it could then be instantiated *on demand*
            // (i.e. member kept null until actually needed)
            // since uses of FileRepository are few and far in between.
            // An idea would be to provide an outer replacement *wrapper* class for FileRepository
            // which has same-interface yet knows how to supply the exact URL to be used.
            // OTOH one also needs to keep in mind the dynamic-configuration aspect:
            // A change of Configuration.TfsProxyUrl should be reflected in changed behaviour instantly,
            // which the current ad-hoc implementation properly does guarantee.
            // So - hmm...
            return webTransferService.DownloadBytes(GetDownloadUrl(item.DownloadUrl, repositoryUuid), credentials);
        }

        /// <summary>
        /// OUTDATED (non-asynchronous i.e. blocking,
        /// and strange foreign-type param dependency) API variant, DO NOT USE.
        /// </summary>
        public virtual void ReadFileAsync(ItemMetaData item, Guid repositoryUuid)
        {
            byte[] data = GetFile(item, repositoryUuid);
            item.ContentDataAdopt(data);
        }

        private static string GetDownloadUrl(string downloadUrl, Guid repositoryUuid)
        {
            string newDownloadUrl = downloadUrl;
            // FIXME: perhaps this (non-)proxy URL switch evaluation
            // could (and then ought to) be relegated
            // to properly generic implementation
            // (use of IRegistrationService - its internal handling) as well?
            if (!string.IsNullOrEmpty(Configuration.TfsProxyUrl))
            {
                newDownloadUrl = Configuration.TfsProxyUrl + "/VersionControlProxy/" + downloadUrl.Substring(downloadUrl.IndexOf("/VersionControl/") + 16);
                newDownloadUrl += "&rid=" + repositoryUuid.ToString();
            }
            return newDownloadUrl;
        }
    }
}
