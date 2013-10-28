using System;
using System.Collections.Generic;
using System.Text;
using SvnBridge.SourceControl;
using System.Net;
using SvnBridge.Interfaces;
using CodePlex.TfsLibrary.ObjectModel;
using System.Threading;
using SvnBridge.Exceptions;
using SvnBridge.Net;
using SvnBridge.Utility;
using SvnBridge.Cache;

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

        public virtual byte[] GetFile(ItemMetaData item, Guid repositoryUuid)
        {
            return webTransferService.DownloadBytes(GetDownloadUrl(item.DownloadUrl, repositoryUuid), credentials);
        }

        public virtual void ReadFileAsync(ItemMetaData item, Guid repositoryUuid)
        {
            byte[] data = GetFile(item, repositoryUuid);
            item.Base64DiffData = SvnDiffParser.GetBase64SvnDiffData(data);
            item.Md5Hash = Helper.GetMd5Checksum(data);
            item.DataLoaded = true;
        }

        private string GetDownloadUrl(string downloadUrl, Guid repositoryUuid)
        {
            string newDownloadUrl = downloadUrl;
            if (!string.IsNullOrEmpty(Configuration.TfsProxyUrl))
            {
                newDownloadUrl = Configuration.TfsProxyUrl + "/VersionControlProxy/" + downloadUrl.Substring(downloadUrl.IndexOf("/VersionControl/") + 16);
                newDownloadUrl += "&rid=" + repositoryUuid.ToString();
            }
            return newDownloadUrl;
        }
    }
}
