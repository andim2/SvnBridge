using System;
using System.Collections;
using System.Web;

namespace SvnBridge.Net
{
	public static class RequestCache
	{
		[ThreadStatic] private static IDictionary currentItems;
	    private static bool? runningInIIS;

	    public static void Init()
		{
			currentItems = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
		}

        public static void Dispose()
        {
            currentItems = null;
        }

		public static IDictionary Items
		{
			get
			{
			    if (RunningInIIS)
					return HttpContext.Current.Items;
                
                EnsureInitialized();
                return currentItems;
			}
		}

	    private static bool RunningInIIS
	    {
	        get
	        {
                if (runningInIIS == null)
                    runningInIIS = HttpContext.Current != null;

	            return runningInIIS.Value;
	        }
	    }

		public static bool IsInitialized
		{
			get
			{
				return RunningInIIS || currentItems != null;
			}
		}

		public static void EnsureInitialized()
		{
            if (!RunningInIIS && currentItems == null)
				throw new InvalidOperationException("Cannot use RequestCache if it wasn't initialized");
		}
	}
}