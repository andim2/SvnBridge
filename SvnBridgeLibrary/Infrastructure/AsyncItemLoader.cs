using System; // IntPtr.Size
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.SourceControl;
using SvnBridge.Utility; // Helper.CooperativeSleep(), Helper.DebugUsefulBreakpointLocation()

namespace SvnBridge.Infrastructure
{
    public /* no "sealed" here (class subsequently derived by Tests) */ class AsyncItemLoader
    {
        private readonly FolderMetaData folderInfo;
        private readonly TFSSourceControlProvider sourceControlProvider;
        private readonly long cacheTotalSizeLimit;
        private bool cancelOperation /* = false */;

        public AsyncItemLoader(FolderMetaData folderInfo, TFSSourceControlProvider sourceControlProvider, long cacheTotalSizeLimit)
        {
            this.folderInfo = folderInfo;
            this.sourceControlProvider = sourceControlProvider;
            this.cacheTotalSizeLimit = cacheTotalSizeLimit;
        }

        public void Start()
        {
            ReadItemsInFolder(folderInfo);
        }

        public virtual void Cancel()
        {
            cancelOperation = true;
        }

        private void ReadItemsInFolder(FolderMetaData folder)
        {
            foreach (ItemMetaData item in folder.Items)
            {
                // Before reading further data, verify total pending size:

                // Wanted to move size check/data reading into a helper,
                // but then the cancel handling below
                // would have to be implemented in an awkward more indirect way...

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

                    if (cancelOperation)
                        break;

                    if (++retry > timeoutInSeconds)
                    {
                        ReportErrorItemDataConsumptionTimeout();
                    }

                    // Do some waiting until hopefully parts of totalLoadedItemsSize
                    // got consumed (by consumer side, obviously).
                    Helper.CooperativeSleep(1000);

                    if (cancelOperation)
                        break;
                }

                if (cancelOperation)
                    break;

                if (item.ItemType == ItemType.Folder)
                {
                    ReadItemsInFolder((FolderMetaData) item);
                }
                else if (!(item is DeleteMetaData))
                {
                    sourceControlProvider.ReadFileAsync(item);
                }
            }
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
        /// to allow for a reliable wait on an item to have achieved "loaded" state.
        /// </summary>
        /// <param name="item">Item whose data we will be waiting for to have finished loading</param>
        /// <param name="spanTimeout">Expiry timeout for waiting for the item's data to become loaded</param>
        public bool WaitForItemLoaded(
            ItemMetaData item,
            TimeSpan spanTimeout)
        {
            DateTime timeUtcWaitLoaded_Start = DateTime.UtcNow; // Calculate ASAP (to determine timeout via precise right-upon-start timestamp)
            DateTime timeUtcWaitLoaded_Expire = DateTime.MinValue;
            TimeSpan spanTimeoutRemain = spanTimeout;

            for (;;)
            {
                // IMPORTANT: definitely remember to do an *initial* status check
                // directly prior to first wait.
                if (item.DataLoaded)
                {
                    break;
                }

                // Since we don't have a wait handle here,
                // need to use fixed short intervals
                // to ensure that we notice changes sufficiently quickly:
                Helper.CooperativeSleep(100);

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

                // Make sure to have handling be focussed on a precise final timepoint
                // to have a precisely bounded timeout endpoint
                // (i.e., avoid annoying accumulation of added-up multi-wait scheduling delays imprecision).
                spanTimeoutRemain = timeUtcWaitLoaded_Expire - timeUtcNow;

                bool isTimeout = (spanTimeoutRemain.CompareTo(TimeSpan.Zero) < 0);
                if (isTimeout)
                {
                    break;
                }
            }

            return item.DataLoaded;
        }
    }
}
