using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace CodePlex.TfsLibrary.ObjectModel.Util
{
	public class DownloadBytesReadState
	{
		private readonly byte[] buffer;
		private readonly int bufferSize;
		private readonly List<byte> downloadedBytes = new List<byte>();
		private readonly WebResponse response;
		private readonly DownloadBytesAsyncResult result;
		public readonly Stream Stream;

		public DownloadBytesReadState(DownloadBytesAsyncResult result, WebResponse response, Stream stream, int bufferSize)
		{
			this.result = result;
			this.response = response;
			Stream = stream;
			this.bufferSize = bufferSize;
			buffer = new byte[bufferSize];
		}
		public void Start(Stream stream)
		{
			stream.BeginRead(
					buffer,
					0,
					bufferSize,
					ReadCallback,
					this);
		}
		public void ReadCallback(IAsyncResult ar)
		{
			try
			{
				int read = Stream.EndRead(ar);
        ListAppendArrayPart(downloadedBytes, buffer, read);
				if (read == 0)
				{
					DisposeResources();

					result.Buffer = downloadedBytes.ToArray();
					downloadedBytes.Clear();
					result.SetComplete();
					return;
				}
				Stream.BeginRead(
					buffer,
					0,
					bufferSize,
					ReadCallback,
					null);
			}
			catch (Exception e)
			{
				result.Exception = e;
				result.SetComplete();
			}
		}

		private void DisposeResources()
		{
			try
			{
				using (response)
				using (Stream)
					return;
			}
			catch
			{
				// ignore exceptions here, we already
				// got all we needed
			}
		}

        private static void ListAppendArrayPart(List<byte> list, byte[] data, int count)
        {
            var requiredMinimumCapacity = list.Count + count;
            ListEnsureCapacity(list, requiredMinimumCapacity);
            for (int i = 0; i < count; ++i)
            {
                list.Add(data[i]);
            }
        }

        private static void ListEnsureCapacity(List<byte> list, int requiredMinimumCapacity)
        {
            bool needEnlargeCapacity = (list.Capacity < requiredMinimumCapacity);
            if (needEnlargeCapacity)
            {
                list.Capacity = requiredMinimumCapacity;
            }
        }
	}
}
