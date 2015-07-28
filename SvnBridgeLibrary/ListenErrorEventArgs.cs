using System;

namespace SvnBridge.Net
{
    public sealed class ListenErrorEventArgs : EventArgs
    {
        private readonly Exception exception;

        public ListenErrorEventArgs(Exception ex)
        {
            exception = ex;
        }

        public Exception Exception
        {
            get { return exception; }
        }
    }
}