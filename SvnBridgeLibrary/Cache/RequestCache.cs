using System;
using System.Collections;
using System.Web;

namespace SvnBridge.Net
{
    /// <summary>
    /// Exceedingly simple (since cleanly symmetrically scoped) helper
    /// with the sole purpose
    /// of ensuring proper handling of RequestCache lifetime.
    /// All uses of this class *must* be done
    /// via properly scope-restricting "using" scope.
    /// </summary>
    /// WARNING: this code might easily be
    /// an insufficient implementation of the woefully horrible IDisposable -
    /// for details, see
    /// "IDisposable: What Your Mother Never Told You About Resource Deallocation"
    ///   http://www.codeproject.com/KB/dotnet/idisposable.aspx
    /// http://www.codeproject.com/Messages/1840365/Re-Definitive-IDisposable-reference.aspx
    public sealed class RequestCache_Scope : IDisposable
    {
        public RequestCache_Scope()
        {
            RequestCache.Init();
        }

        public void Dispose()
        {
            // DEBUG_SITE: may examine full content of cache here
            // (directly prior to its disposal).

            RequestCache.Dispose();

            // Hmm, do we need to call GC.SuppressFinalize() here?
            // http://joeduffyblog.com/2005/04/08/dg-update-dispose-finalization-and-resource-management/
        }
    }

    /// <summary>
    /// Global, static cache class
    /// the lifetime of which is (and needs to remain!) exactly restricted
    /// to per-HTTP-request scope.
    /// </summary>
	public static class RequestCache
	{
        // IMPORTANT NOTE:
        // I originally thought
        // that we might have a grave violation/conflict
        // of per-session-separate instance data
        // (since this is a static class which is shared
        // between concurrent multi-user network request activity),
        // however it seems that the ThreadStatic (TLS) attribute here
        // is exactly the means to achieve properly fully provided
        // per-request (which likely is == per-thread)
        // instance data separation.
        // https://larryparkerdotnet.wordpress.com/2009/11/29/thread-local-storage-the-easy-way/
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

    /// <summary>
    /// Provides specific well-known key strings to be used by RequestCache users.
    /// Since a Hashtable is generic (type-agnostic), we cannot create local helper methods
    /// to query/set specific RequestCache content (their signature would not be type-agnostic).
    /// Thus at least have some well-known string constants
    /// in order to minimize danger of typos in error-prone open-coded string literals.
    /// It could be considered somewhat of a layer violation
    /// to be offering *specific* (interface-user-related) key values
    /// at the same location
    /// that the *basic/generic* class gets offered - XXX?
    /// </summary>
    public static class RequestCache_Keys
    {
        public const string RequestBody = "RequestBody";
    }
}
