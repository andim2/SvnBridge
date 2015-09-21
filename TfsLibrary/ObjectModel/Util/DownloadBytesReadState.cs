using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace CodePlex.TfsLibrary.ObjectModel.Util
{
	public class DownloadBytesReadState
	{
		private readonly int bufferSize;
		private readonly byte[] buffer;
		private readonly List<byte> downloadedBytes = new List<byte>();
		private readonly WebResponse response;
		private readonly DownloadBytesAsyncResult result;
		public readonly Stream Stream;

		public DownloadBytesReadState(DownloadBytesAsyncResult result, WebResponse response, Stream stream, int expectedContentLength)
		{
			this.result = result;
			this.response = response;
			Stream = stream;
			this.bufferSize = expectedContentLength;
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
            // WARNING: below issue managed the incredible feat
            // of causing > 5GB total process space!!!!
            // However the kicker is
            // that in fact we do not need to do any size handling here whatsoever
            // since in this particular case we have a List
            // (where its .Add() will manage .Capacity automatically).
#if false
            // AWFUL Capacity handling!! (GC catastrophy)
            // .Capacity value should most definitely *NEVER* be directly (manually) modified,
            // since framework ought to know best
            // how to increment .Capacity value in suitably future-proof-sized steps!
            // (read: it's NOT useful
            // to keep incrementing [read: keep actively reallocating!!]
            // a continuously aggregated perhaps 8MB .Capacity
            // by some perhaps 4273 Bytes each!)
            bool needEnlargeCapacity = (list.Capacity < requiredMinimumCapacity);
            if (needEnlargeCapacity)
            {
                list.Capacity = requiredMinimumCapacity;
            }
#endif
        }
	}
}
