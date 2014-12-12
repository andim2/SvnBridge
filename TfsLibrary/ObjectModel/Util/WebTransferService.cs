using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using CodePlex.TfsLibrary.ObjectModel.Util;
using CodePlex.TfsLibrary.Utility;

namespace CodePlex.TfsLibrary.ObjectModel
{
	public class WebTransferService : IWebTransferService
	{
		private const int READ_BUFFER_SIZE = 65536;
		private readonly IFileSystem fileSystem;

		public WebTransferService(IFileSystem fileSystem)
		{
			this.fileSystem = fileSystem;
		}

		public WebTransferFormData CreateFormPostData()
		{
			return new WebTransferFormData(fileSystem);
		}

		public void Download(string url,
							 ICredentials credentials,
							 string localPath)
		{
			WebRequest request = TfsUtil.SetupWebRequest(WebRequest.Create(url), credentials);

			using (WebResponse response = request.GetResponse())
			{
				using (Stream stream = GetResponseStream(response))
				{
					WriteStreamToFile(stream, localPath);
				}
			}
		}

		public byte[] DownloadBytes(string url,
									ICredentials credentials)
		{
			WebRequest request = TfsUtil.SetupWebRequest(WebRequest.Create(url), credentials);
			using (WebResponse response = request.GetResponse())
			{
				using (Stream stream = GetResponseStream(response))
				{
					List<byte> results = new List<byte>();
					// We can't trust response.ContentLength, we may get a gzip
					// reply, in which case the content legth and the result are 
					// different
					byte[] buffer = new byte[response.ContentLength];
					int current = 0;
					int read;
					do
					{
						read = stream.Read(buffer, current, buffer.Length - current);
						current += read;
						if (current >= buffer.Length)
						{
							results.AddRange(buffer);
							buffer = new byte[response.ContentLength];
							current = 0;
						}
					} while (read != 0);
					if(current!=buffer.Length)
						Array.Resize(ref buffer, current);
					results.AddRange(buffer);
					return results.ToArray();
				}
			}
		}

		public IAsyncResult BeginDownloadBytes(string url, ICredentials credentials, AsyncCallback callback)
		{
			WebRequest request = TfsUtil.SetupWebRequest(WebRequest.Create(url), credentials);
			DownloadBytesAsyncResult result = new DownloadBytesAsyncResult(url, request, callback);
			request.BeginGetResponse(GetResponseCallback, result);
			return result;
		}

		public byte[] EndDownloadBytes(IAsyncResult ar)
		{
			if (ar.IsCompleted == false)
			{
				throw new InvalidOperationException("Called EndDownloadBytes before operation was complete");
			}
			using (DownloadBytesAsyncResult result = (DownloadBytesAsyncResult)ar)
			{
				if (result.Exception != null)
				{
                    WebException we = result.Exception as WebException;
                    if (we != null )
                    {
                        HttpWebResponse response = we.Response as HttpWebResponse;
                        if (response!= null && response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new UnauthorizedAccessException("Could not download file: " + result.Url, result.Exception);
                        }
                    }
				    throw new WebException("Could not download file: " + result.Url, result.Exception);
				}
				byte[] buffer = result.Buffer;
				// need to remove reference to the buffer, because we may hold the reference to the 
				// async result for a while yet
				result.Buffer = null;
				return buffer;
			}
		}

		private static void GetResponseCallback(IAsyncResult ar)
		{
			DownloadBytesAsyncResult result = (DownloadBytesAsyncResult)ar.AsyncState;
			try
			{
				WebResponse response = ((WebRequest)result.AsyncState).EndGetResponse(ar);
				Stream stream = GetResponseStream(response);
				DownloadBytesReadState state =
					new DownloadBytesReadState(result, response, stream, (int)response.ContentLength);
				state.Start(stream);
			}
			catch (Exception e)
			{
				result.Exception = e;
				result.SetComplete();
			}
		}

		private static Stream GetResponseStream(WebResponse response)
		{
			if (string.Compare(response.ContentType, "application/gzip", true) != 0)
			{
				return response.GetResponseStream();
			}

			Stream stream = null;

			try
			{
				stream = response.GetResponseStream();
				return new GZipStream(stream, CompressionMode.Decompress);
			}
			catch
			{
				if (stream != null)
				{
					stream.Dispose();
				}

				throw;
			}
		}

		public void PostForm(string url,
							 ICredentials credentials,
							 WebTransferFormData formData)
		{
			HttpWebRequest request = (HttpWebRequest)TfsUtil.SetupWebRequest(WebRequest.Create(url), credentials);
			request.Method = "POST";
			request.ContentType = "multipart/form-data; boundary=" + formData.Boundary;

			using (Stream stream = request.GetRequestStream())
			{
				formData.Render(stream);
			}

			request.GetResponse().Close();
		}

		private void WriteStreamToFile(Stream stream,
									   string localPath)
		{
			byte[] buffer = new byte[READ_BUFFER_SIZE];

			fileSystem.EnsurePath(fileSystem.GetDirectoryName(localPath));

			using (Stream fs = fileSystem.OpenFile(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				do
				{
					int bytesRead = stream.Read(buffer, 0, buffer.Length);
					if (bytesRead == 0)
					{
						break;
					}
					fs.Write(buffer, 0, bytesRead);
				} while (true);
			}
		}
	}
}
