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
			this.bufferSize = DetermineSizeOfIncrementalReadBuffer(expectedContentLength);
			buffer = new byte[bufferSize];
		}

        /// <summary>
        /// While we used to be using
        /// a weird full-HTTP-ContentLength-sized buffer
        /// for per-incremental-callback reads,
        /// we cannot keep doing so since
        /// ContentLength may be infinitely big
        /// (at least for all cases other than chunked Transfer-Encoding
        /// where ContentLength will be passed as 0, that is)
        /// and will currently be stupidly used here
        /// to collect a final big-blob object already at this place
        /// (rather than forwarding things
        /// in a fully incremental streamy manner until the
        /// final destination amasses big-blob!!!),
        /// thus we have awful memory size constraint conditions
        /// which means we should at least
        /// keep the incremental buffer minimal.
        /// </summary>
        /// <remarks>
        /// Chose a size
        /// which is below GC LOH threshold size,
        /// yet large enough to be much larger
        /// than the usual incremental download lengths.
        /// </remarks>
        /// <param name="contentLength"></param>
        /// <returns></returns>
        private static int DetermineSizeOfIncrementalReadBuffer(int contentLength)
        {
            return Math.Min(contentLength, 64 * 1024);
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

        /// <remarks>
        /// Ugh, this is using a raw loop
        /// rather than using something like .AddRange().
        /// Can't use .AddRange() since that operates on whole arrays only
        /// (and even ArraySegment which is said to segmentize arrays
        /// will NOT work properly
        /// since the *whole* content of the backing .Array
        /// *will* still be copied).
        /// And we most certainly don't want
        /// to create a (properly length-correct)
        /// partial Array from original array
        /// via ugly copying.
        /// Alternative would be LINQ stuff
        /// (which is a version dependency
        /// which I'd dearly want to avoid)
        /// or hand-crafting our own IEnumerable-supporting wrapper
        /// (in which case it most definitely would be no faster
        /// than the short manual loop here).
        /// Or perhaps get rid of target List
        /// and use a MemoryStream instead -
        /// but that seems to bring its own can of worms ermmm problems
        /// (see e.g. http://www.codeproject.com/Articles/348590/A-replacement-for-MemoryStream ).
        /// Potentially helpful references:
        /// http://stackoverflow.com/questions/582550/c-sharp-begin-endreceive-how-do-i-read-large-data
        /// http://stackoverflow.com/questions/406485/array-slices-in-c-sharp
        /// http://stackoverflow.com/questions/733243/how-to-copy-part-of-an-array-to-another-array-in-c
        /// http://stackoverflow.com/questions/5756692/arraysegment-returning-the-actual-segment-c-sharp
        /// "Copy data from Array to List"
        ///   http://www.databaseforum.info/25/676730.aspx
        /// </remarks>
        private static void ListAppendArrayPart(List<byte> list, byte[] data, int count)
        {
            var listCapacity = list.Capacity; // debug helper (remember old value)
            var listCount = list.Count; // debug helper (remember old value)
            var requiredMinimumCapacity = listCount + count;
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
