using System;
using System.Threading;

namespace CodePlex.TfsLibrary.ObjectModel.Util
{
	public class DownloadBytesAsyncResult : IAsyncResult, IDisposable
	{
		private readonly AsyncCallback callback;
		private readonly ManualResetEvent asyncWaitHandle = new ManualResetEvent(false);
		private readonly string url;
		private readonly object asyncState;
		private bool isCompleted;
		private bool wasDisposed;
		private byte[] buffer;
		private Exception exception;

		public string Url
		{
			get { return url; }
		}

		public DownloadBytesAsyncResult(string url, object asyncState, AsyncCallback callback)
		{
			this.url = url;
			this.callback = callback;
			this.asyncState = asyncState;
		}

		public void SetComplete()
		{
			isCompleted = true;
			try
			{
				if (callback != null)
					callback(this);
			}
			finally
			{
				asyncWaitHandle.Set();
			}
		}

		public bool IsCompleted
		{
			get { return isCompleted; }
		}

		public WaitHandle AsyncWaitHandle
		{
			get { return asyncWaitHandle; }
		}

		public object AsyncState
		{
			get { return asyncState; }
		}

		public bool CompletedSynchronously
		{
			get { return false; }
		}

		public byte[] Buffer
		{
			get { return buffer; }
			set { buffer = value; }
		}

		public Exception Exception
		{
			get { return exception; }
			set { exception = value; }
		}

		public void Dispose()
		{
			if (wasDisposed == false)
				return;
			asyncWaitHandle.Close();
			GC.SuppressFinalize(this);
			wasDisposed = true;
		}

		~DownloadBytesAsyncResult()
		{
			asyncWaitHandle.Close();
		}
	}
}
