using System; // IntPtr.Size
using System.Threading; // AutoResetEvent
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.SourceControl;
using SvnBridge.Utility; // Helper.CooperativeSleep(), Helper.DebugUsefulBreakpointLocation()

namespace SvnBridge.Infrastructure
{
    public sealed class AsyncItemLoaderExceptionCancel : Exception
    {
    }

    public /* no "sealed" here (class subsequently derived by Tests) */ class AsyncItemLoader
    {
        private readonly FolderMetaData folderInfo;
        private readonly TFSSourceControlProvider sourceControlProvider;
        private readonly long cacheTotalSizeLimit;
        private bool cancelOperation /* = false */;
        private readonly AutoResetEvent finishedOne;
        private readonly WaitHandle[] finishedOneArray;

        public AsyncItemLoader(FolderMetaData folderInfo, TFSSourceControlProvider sourceControlProvider, long cacheTotalSizeLimit)
        {
            this.folderInfo = folderInfo;
            this.sourceControlProvider = sourceControlProvider;
            this.cacheTotalSizeLimit = cacheTotalSizeLimit;
            // Performance: do allocation of event array on init rather than per-use.
            // And keep specific member for handle itself, too,
            // to enable direct (non-dereferenced) fast access.
            this.finishedOne = new AutoResetEvent(false);
            this.finishedOneArray = new WaitHandle[] { finishedOne };
        }

        public void Start()
        {
            try
            {
                ReadItemsInFolder(folderInfo);
            }
            catch (AsyncItemLoaderExceptionCancel)
            {
                // Nothing to be done other than cleanly bailing out
            }
        }

        public virtual void Cancel()
        {
            cancelOperation = true;
        }

        private void CheckCancel()
        {
            if (cancelOperation)
            {
                Helper.DebugUsefulBreakpointLocation();
                throw new AsyncItemLoaderExceptionCancel();
            }
        }

        private void ReadItemsInFolder(FolderMetaData folder)
        {
            foreach (ItemMetaData item in folder.Items)
            {
                // Before reading further data, verify total pending size:

                bool haveUnusedItemLoadBufferCapacity = WaitForUnusedItemLoadBufferCapacity();

                if (!(haveUnusedItemLoadBufferCapacity))
                {
                    break;
                }

                CheckCancel();

                if (item.ItemType == ItemType.Folder)
                {
                    ReadItemsInFolder((FolderMetaData) item);
                }
                else if (!(item is DeleteMetaData))
                {
                    sourceControlProvider.ReadFileAsync(item);
                    NotifyConsumer_ItemProcessingEnded(item);
                }
            }
        }

        private bool WaitForUnusedItemLoadBufferCapacity()
        {
                bool haveUnusedItemLoadBufferCapacity = false;

                // Q&D HACK to ensure that this crawler resource will bail out at least eventually
                // in case of a problem (e.g. missing consumer-side fetching).
                long timeoutInSeconds = TimeoutAwaitAnyConsumptionActivity;
                long retry = 0;
                for (; ; )
                {
                    var totalLoadedItemsSize = CalculateLoadedItemsSize(folderInfo);
                    haveUnusedItemLoadBufferCapacity = HaveUnusedItemLoadBufferCapacity(totalLoadedItemsSize);
                    if (haveUnusedItemLoadBufferCapacity)
                    {
                        break;
                    }

                    CheckCancel();

                    if (++retry > timeoutInSeconds)
                    {
                        ReportErrorItemDataConsumptionTimeout();
                    }

                    // Do some waiting until hopefully parts of totalLoadedItemsSize
                    // got consumed (by consumer side, obviously).
                    Helper.CooperativeSleep(1000);

                    CheckCancel();
                }

                return haveUnusedItemLoadBufferCapacity;
        }

        private bool HaveUnusedItemLoadBufferCapacity(long totalLoadedItemsSize)
        {
            bool haveUnusedItemLoadBufferCapacity = false;

            var unusedItemLoadBufferCapacity = (CacheTotalSizeLimit - totalLoadedItemsSize);

            if (0 < unusedItemLoadBufferCapacity)
            {
                haveUnusedItemLoadBufferCapacity = true;
            }

            return haveUnusedItemLoadBufferCapacity;
        }

        /// <remarks>
        /// A timeout of 4 hours ought to be more than enough
        /// to expect a client
        /// (which simply is waiting for us to produce things,
        /// as opposed to us having to go through *hugely* complex
        /// TFS <-> SVN conversion processes)
        /// to have fetched data.
        ///
        /// Well, hmm, but OTOH in pathological cases
        /// even this timeout *will* get exceeded
        /// since some very large requests (>10M total) may happen
        /// where the consumer is waiting for the last parts -
        /// crawler will hit limit, and consumer will never get its last file served.
        /// We will have to drastically rework things to handle file crawling
        /// in a more robust way.
        /// </remarks>
        private static long TimeoutAwaitAnyConsumptionActivity
        {
            get { return 3600*4; }
        }

        /// <summary>
        /// Notifies the consumer side
        /// (well, at least once the time comes
        /// that we do want to let the consumer side
        /// learn of the new [and final] item state)
        /// that processing (i.e., download activities)
        /// of this item ultimately ended
        /// (irrespective of whether successful *or* not).
        /// </summary>
        private void NotifyConsumer_ItemProcessingEnded(ItemMetaData item)
        {
            NotifyConsumer(); // new item available
        }

        private void NotifyConsumer()
        {
            finishedOne.Set();
        }

        /// <summary>
        /// Queries total data size of all items within the entire directory hierarchy
        /// (i.e. file content data that we gathered and that awaits retrieval by client side).
        /// </summary>
        /// <param name="folder">Base folder to calculate the hierarchical data items size of</param>
        /// <returns>Byte count currently occupied by data items below the base folder</returns>
        private long CalculateLoadedItemsSize(FolderMetaData folder)
        {
            long itemsSize = 0;

            foreach (ItemMetaData item in folder.Items)
            {
                if (item.ItemType == ItemType.Folder)
                {
                    itemsSize += CalculateLoadedItemsSize((FolderMetaData) item);
                }
                else if (item.DataLoaded)
                {
                    itemsSize += item.Base64DiffData.Length;
                }
            }
            return itemsSize;
        }

        private long CacheTotalSizeLimit
        {
            get
            {
                return cacheTotalSizeLimit;
            }
        }

        private static void ReportErrorItemDataConsumptionTimeout()
        {
            Helper.DebugUsefulBreakpointLocation();
            throw new TimeoutException("Timeout while waiting for consumption of filesystem item data");
        }

        /// <summary>
        /// Helper for the *consumer*-side thread context,
        /// to allow for reliable waiting and fetching of item data
        /// (after item has achieved "loaded" state).
        /// </summary>
        /// <param name="item">Item whose data we will be waiting for to have finished loading</param>
        /// <param name="spanTimeout">Expiry timeout for waiting for the item's data to become loaded</param>
        /// <param name="base64DiffData">Receives the base64-encoded diff data</param>
        /// <param name="md5Hash">Receives the MD5 hash which had been calculated the moment the data has been stored (ensure end-to-end validation)</param>
        public bool TryRobItemData(
            ItemMetaData item,
            TimeSpan spanTimeout,
            out string base64DiffData,
            out string md5Hash)
        {
            bool gotData = false;

            gotData = WaitForItemLoaded(
                item,
                spanTimeout);

            if (gotData)
            {
                base64DiffData = DoRobItemData(
                    item,
                    out md5Hash);
            }
            else
            {
                base64DiffData = "";
                md5Hash = "";
            }

            return gotData;
        }

        private bool WaitForItemLoaded(
            ItemMetaData item,
            TimeSpan spanTimeout)
        {
            DateTime timeUtcWaitLoaded_Start = DateTime.UtcNow; // Calculate ASAP (to determine timeout via precise right-upon-start timestamp)
            DateTime timeUtcWaitLoaded_Expire = DateTime.MinValue;
            TimeSpan spanTimeoutRemain = spanTimeout;

            // Since the event handle currently is loader-global (and probably will remain,
            // since that ought to be more efficient than per-item handles horror),
            // it will be used to signal *any* progress.
            // Thus we need to keep iterating until in fact *our* item is loaded.
            // And since we need to do that, update a timeout value
            // which will be reliably determined from actual current timestamp.

            for (;;)
            {
                // IMPORTANT: definitely remember to do an *initial* status check
                // directly prior to first wait.
                if (item.DataLoaded)
                {
                    break;
                }

                // FIXME!! race window *here*:
                // if producer happens to be doing [set .DataLoaded true and signal event] *right here*,
                // then prior .DataLoaded false check will have failed
                // and we're about to wait on an actually successful load
                // (and having missed the signal event).
                // The properly atomically scoped (read: non-racy) solution likely would be
                // to move .DataLoaded evaluation inside a Monitor scope
                // (and below wait activity would then unlock the Monitor),
                // but since finishedOneArray is currently managed separately from a Monitor scope
                // properly handshaked unlocked-waiting might be not doable ATM.

                // Cannot use .WaitOne() since that one does not signal .WaitTimeout (has bool result).
                int idxEvent = WaitHandle.WaitAny(finishedOneArray, spanTimeoutRemain);
                bool isTimeout = (WaitHandle.WaitTimeout == idxEvent);
                if (isTimeout)
                {
                    break;
                }

                if (item.DataLoaded)
                {
                    break;
                }

                // Performance: nice trick: do expensive calculation of expiration stamp
                // only *after* the first wait has already been done :)
                bool expire_needs_init = (DateTime.MinValue == timeUtcWaitLoaded_Expire);
                if (expire_needs_init)
                {
                    timeUtcWaitLoaded_Expire = timeUtcWaitLoaded_Start + spanTimeout;
                }

                // Performance: implement grabbing current timestamp ALAP, to have strict timeout-side handling.
                DateTime timeUtcNow = DateTime.UtcNow; // debug helper

                // Make sure to have the timeout value variable updated for next round above:
                // And make sure to have handling be focussed on a precise final timepoint
                // to have a precisely bounded timeout endpoint
                // (i.e., avoid annoying accumulation of added-up multi-wait scheduling delays imprecision).
                spanTimeoutRemain = timeUtcWaitLoaded_Expire - timeUtcNow;
            }

            return item.DataLoaded;
        }

        private string DoRobItemData(
            ItemMetaData item,
            out string md5Hash)
        {
            string base64DiffData;

            base64DiffData = item.ContentDataRobAsBase64(
                out md5Hash);

            return base64DiffData;
        }
    }
}
