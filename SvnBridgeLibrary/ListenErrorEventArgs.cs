using System;

namespace SvnBridge.Net
{
    public class ListenErrorEventArgs : EventArgs
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