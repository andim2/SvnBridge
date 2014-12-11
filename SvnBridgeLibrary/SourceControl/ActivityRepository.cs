using System;
using System.Collections.Generic;
using System.Threading;
using SvnBridge.SourceControl.Dto;

namespace SvnBridge.SourceControl
{
	public static class ActivityRepository
	{
		private static readonly Dictionary<string, DateTime> activitiesTimeStamps = new Dictionary<string, DateTime>();
		private static readonly Dictionary<string, Activity> activities = new Dictionary<string, Activity>();
		private static readonly ReaderWriterLock rwLock = new ReaderWriterLock();

		private static readonly Timer timer = new Timer(ActivitiesCleanup, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

		private static void ActivitiesCleanup(object state)
		{
			//this is here to prevent a compiler warning about the timer variable not used
			//it does absolutely nothing and has no meaning whatsoever
			timer.GetHashCode();

			rwLock.AcquireWriterLock(Timeout.Infinite);
			try
			{
				foreach (KeyValuePair<string, DateTime> pair in new Dictionary<string, DateTime>(activitiesTimeStamps))
				{
					//It is not likely that a commit would last more than 24 hours
					if((DateTime.Now-pair.Value).TotalHours > 24)
						Delete(pair.Key);
				}
			}
			finally
			{
				rwLock.ReleaseWriterLock();
			}
		}

		public static void Create(string activityId)
		{
			rwLock.AcquireWriterLock(Timeout.Infinite);
			try
			{
				activities[activityId] = new Activity();
				activitiesTimeStamps[activityId] = DateTime.Now;
			}
			finally
			{
				rwLock.ReleaseWriterLock();
			}
		}

		public static void Delete(string activityId)
		{
			bool upgradedToWriterLcok = false;
			var writerLock = new LockCookie();
			try
			{
				if (rwLock.IsReaderLockHeld)
				{
					writerLock = rwLock.UpgradeToWriterLock(Timeout.Infinite);
					upgradedToWriterLcok = true;
				}
				else
				{
					rwLock.AcquireWriterLock(Timeout.Infinite);
				}
			
				activities.Remove(activityId);
				activitiesTimeStamps.Remove(activityId);
			}
			finally
			{
				if(upgradedToWriterLcok)
					rwLock.DowngradeFromWriterLock(ref writerLock);
				else
					rwLock.ReleaseWriterLock();
			}
		}

		public static void Use(string activityId, Action<Activity> action)
		{
			rwLock.AcquireReaderLock(Timeout.Infinite);
			try
			{
				Activity activity;
				if(activities.TryGetValue(activityId, out activity)==false)
					throw new InvalidOperationException("Could not find activity id: " + activityId);
				lock(activity)
				{
					action(activity);
				}
			}
			finally 
			{
				rwLock.ReleaseReaderLock();
			}
		}

		public static bool Exists(string activityId)
		{
			rwLock.AcquireReaderLock(Timeout.Infinite);
			try
			{
				return activities.ContainsKey(activityId);
			}
			finally
			{
				rwLock.ReleaseReaderLock();
			}
		}
	}
}