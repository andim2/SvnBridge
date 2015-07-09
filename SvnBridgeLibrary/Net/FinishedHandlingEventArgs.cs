using System;
using SvnBridge.Handlers;

namespace SvnBridge.Net
{
    public class FinishedHandlingEventArgs : EventArgs
    {
        public readonly TimeSpan Duration;
        public readonly string Url;
        public readonly string Method;

        public FinishedHandlingEventArgs(TimeSpan duration, string url, string method)
        {
            Duration = duration;
            Url = url;
            Method = method;
        }
    }
}