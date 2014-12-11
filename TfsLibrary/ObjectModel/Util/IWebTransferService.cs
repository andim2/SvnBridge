using System;
using System.Net;

namespace CodePlex.TfsLibrary.ObjectModel
{
    public interface IWebTransferService
    {
        WebTransferFormData CreateFormPostData();

        void Download(string url,
                      ICredentials credentials,
                      string localPath);

        byte[] DownloadBytes(string url,
                             ICredentials credentials);

        IAsyncResult BeginDownloadBytes(string url,
                                        ICredentials credentials,
                                        AsyncCallback callback);

        byte[] EndDownloadBytes(IAsyncResult ar);

        void PostForm(string url,
                      ICredentials credentials,
                      WebTransferFormData formData);
    }
}
