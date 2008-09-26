using System;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.PathParsing;

namespace SvnBridge.Stubs
{
    public delegate void StartDelegate();

    public delegate void StopDelegate();

    public class StubListener : Listener
    {
        public int Get_Port;
        public int Set_Port;
        public bool Start_Called;
        public StartDelegate Start_Delegate;
        public bool Stop_Called;
        public StopDelegate Stop_Delegate;

        public StubListener() : base(null, null) { }

        public override event EventHandler<ListenErrorEventArgs> ListenError = delegate { };
        
        public override event EventHandler<FinishedHandlingEventArgs> FinishedHandling
        {
            add { }
            remove { }
        }

        public override int Port
        {
            get { return Get_Port; }
            set { Set_Port = value; }
        }

        public override void Start(IPathParser parser)
        {
            if (Start_Delegate != null)
            {
                Start_Delegate();
            }

            Start_Called = true;
        }

        public override void Stop()
        {
            if (Stop_Delegate != null)
            {
                Stop_Delegate();
            }
            Stop_Called = true;
        }

        public bool ListenErrorHasDelegate()
        {
            return ListenError != null;
        }

        public void RaiseListenErrorEvent(string message)
        {
            ListenError(this, new ListenErrorEventArgs(new Exception(message)));
        }
    }
}