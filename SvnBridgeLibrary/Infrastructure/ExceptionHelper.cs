using System;
using System.Reflection;

namespace SvnBridge.Infrastructure
{
    public class ExceptionHelper
    {
        private static readonly MethodInfo preserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace",
                                                                                   BindingFlags.NonPublic | BindingFlags.Instance);
        public static void PreserveStackTrace(Exception exception)
        {
            preserveStackTrace.Invoke(exception, new object[0]);
        }
    }
}