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
		private readonly DataStoreSegmented dataStore;
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
      // GRAVE NOTE: passing a specific intended capacity to a List ctor
      // (rather than no parameter or 0)
      // will disable proper implicit .Capacity growing
      // via .Capacity duplication
      // and instead resorts to single-byte increment!
      // (this behaviour change most likely is indicated by
      // System.Collections.IList.IsFixedSize
      // - see also
      // https://social.msdn.microsoft.com/Forums/en-US/d7dbaa97-eb40-4d70-a0b5-fee2189a6716/ilist-isfixedsize-property
      // )
      // But since content length may be very huge
      // which will necessitate copying of HUGE data,
      // better do prefer to pass the correctly-predicted(?) content length
      // to avoid extremely painful huge-blob duplication...
      var initialCapacity = ChooseSuitableCapacityForFinalContentLength(expectedContentLength);
      dataStore = new DataStoreSegmented(initialCapacity);
		}

        /// <summary>
        /// Since target buffer
        /// will also roughly (gzip or not...)
        /// end up with content size,
        /// do choose initial capacity to be something *slightly* larger
        /// (do achieve some sizeable reserve .Capacity,
        /// yet avoid using excessive memory in huge-blob case).
        /// </summary>
        private static int ChooseSuitableCapacityForFinalContentLength(int expectedContentLength)
        {
            int chosenCapacity;

            chosenCapacity = expectedContentLength;

            ChooseSuitableCapacity_Increased(ref chosenCapacity);

            ChooseSuitableCapacity_Align(ref chosenCapacity);

            return chosenCapacity;
        }

        private static void ChooseSuitableCapacity_Increased(ref int capacity)
        {
            int increasedCapacity =
                (capacity) +
                (capacity / 32); // ~3% leeway

        }

        private static void ChooseSuitableCapacity_Align(ref int capacity)
        {
            bool doHeapAlign = DoHeapAlignOfContentBlock();
            if (doHeapAlign)
            {
                ChooseSuitableCapacity_AlignDo(ref capacity);
            }
        }

        /// Nope, we will NOT do manual "heap block alignment" here -
        /// there's not a single relevant shred
        /// of *domain-specific* hints about memory handling
        /// that we are able to contribute here,
        /// thus we try to trust in the heap management
        /// doing its job correctly...
        /// (and likely with much higher sophistication)
        private static bool DoHeapAlignOfContentBlock()
        {
            return false;
        }

        private static void ChooseSuitableCapacity_AlignDo(ref int capacity)
        {
            const int alignment = 32 * 1024; // nice align (keep hitting same cache-hot heap blocks!)

            const int alignmentMask = (alignment-1);

            var alignedCapacity = (capacity + alignmentMask) & ~(alignmentMask);
            capacity = alignedCapacity;
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
        dataStore.Append(buffer, read);
				if (read == 0)
				{
					DisposeResources();

					result.Buffer = dataStore.GrabAsArray();
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
	}

    internal interface DataStore // XXX: any standard interface that we ought to obey/implement instead? ICollection?
    {
        void Append(byte[] buffer, int count);

        byte[] GrabAsArray();
    }

    /// <summary>
    /// Segmented-storage (non-linear) data container.
    /// To be used in cases
    /// where final length of huge-blob data
    /// is not reliably known yet
    /// and we don't want to keep having very painful reallocations
    /// (final linear full-length data will be exported *once* at end).
    /// </summary>
    internal class DataStoreSegmented : DataStore
    {
        private readonly List<byte[]> segments;
        private long position;
        private long length;
        private long capacity;

        public DataStoreSegmented(long expectedContentLength)
        {
            var numSegments = PositionToSegmentIndex(expectedContentLength) + 1;
            var segmentsInitialCapacity = numSegments;
            this.segments = new List<byte[]>(segmentsInitialCapacity);
        }

        private static int PositionToSegmentIndex(long position)
        {
            return (int)(position / SegmentSize);
        }

        private static int PositionToSegmentPosition(long position)
        {
            return (int)(position % SegmentSize);
        }

        /// <remarks>
        /// Chose a size
        /// which is below GC LOH threshold size
        /// (keep LOH free from fragmentation,
        /// to remain able to accomodate a final HUGE linear blob),
        /// yet large enough to be much larger
        /// than the usual incremental download lengths.
        /// </remarks>
        private static int SegmentSize
        {
            get
            {
                return 64 * 1024;
            }
        }

        public long Position
        {
            get
            {
                return position;
            }
        }

        public long Length
        {
            get
            {
                return length;
            }
        }

        public long Capacity
        {
            get
            {
                return capacity;
            }
        }

        public virtual void Append(byte[] buffer, int count)
        {
            var sizeSeg = SegmentSize;
            var posBegin = Position;
            var posEnd = posBegin + count;

            var remain = count;

            var pos = posBegin;
            var sourceIndex = 0;
            for (; ; )
            {
                bool haveDataRemain = (remain > 0);
                if (!(haveDataRemain))
                {
                    break;
                }

                var idxSeg = PositionToSegmentIndex(pos);
                EnsureSegmentAvailable(idxSeg);
                var posSeg = PositionToSegmentPosition(pos);
                var remainSeg = (sizeSeg - posSeg);
                var lenThisTime = Math.Min(remain, remainSeg);

                var segment = segments[idxSeg];

                var destinationIndex = posSeg;
                Array.Copy(buffer, sourceIndex, segment, destinationIndex, lenThisTime);

                pos += lenThisTime;
                sourceIndex += lenThisTime;
                remain -= lenThisTime;
            }

            position = pos;
            length = position;
        }

        /// <remarks>
        /// Ensures properly allocated segments *up to* a certain index.
        /// Expects to be called incrementally / evolutionary
        /// (starting with index 0).
        /// </remarks>
        /// <param name="idxSeg"></param>
        private void EnsureSegmentAvailable(int idxSeg)
        {
            for (; ; )
            {
                bool haveEnoughSegments = (segments.Count > idxSeg); // >= idxSeg + 1
                if (haveEnoughSegments)
                {
                    break;
                }

                var sizeSeg = SegmentSize;
                segments.Add(new byte[sizeSeg]);
                capacity += sizeSeg;
            }
        }

        public virtual byte[] GrabAsArray()
        {
            var sizeSeg = SegmentSize;
            var len = Length;
            byte[] array = new byte[len];

            long pos = 0;
            for (;;)
            {
                var remain = (len - pos);
                bool haveDataRemain = (remain > 0);
                if (!(haveDataRemain))
                {
                    break;
                }

                var idxSeg = PositionToSegmentIndex(pos);
                var segment = segments[idxSeg];
                var lenThisTime = Math.Min(remain, sizeSeg);

                Array.Copy(segment, 0, array, pos, lenThisTime);
                segment = null; // GC

                pos += lenThisTime;
            }

            segments.Clear();

            return array;
        }
    }

    internal class DataStoreList : DataStore
    {
        private readonly List<byte> data;

        public DataStoreList(int expectedContentLength)
        {
            this.data = new List<byte>(expectedContentLength);
        }

        public virtual void Append(byte[] buffer, int count)
        {
            ListAppendArrayPart(data, buffer, count);
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
            //ListEnsureCapacityDo(list, requestedMinimumCapacity);
        }

        private static void ListEnsureCapacityDo(List<byte> list, int requiredMinimumCapacity)
        {
            // UPDATE: well, in fact List does *NOT* seem to
            /// implicitly increase .Capacity properly
            // (don't know whether that's the case in general,
            // but at least in certain situations, such as very large .Capacity,
            // List.Add() will cause .Capacity
            // to have positively awful single-byte increments!!!!)
            // .Capacity change most certainly is dependent on (not) passing initialCapacity to ctor:
            // when given initialCapacity, list .Capacity will be assumed to not need increasing,
            // thus the most that will be done is single-byte increments.
#if true // you did construct List to be flexible-capacity (non-initialCapacity), right?
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

        public virtual byte[] GrabAsArray()
        {
            byte[] array;

            array = data.ToArray();
            data.Clear();

            return array;
        }
    }
}
