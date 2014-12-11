using SvnBridge.RequestReceiver;

namespace SvnBridge.Stubs
{
    public class StubRequestReceiver : IRequestReceiver
    {
        internal int Get_Port;
        internal string Get_TfsServerUrl;
        internal int Set_Port;
        internal string Set_TfsServerUrl;
        internal bool Start_Called;
        internal bool Stop_Called;

        #region IRequestReceiver Members

        public int Port
        {
            get { return Get_Port; }
            set { Set_Port = value; }
        }

        public string TfsServerUrl
        {
            get { return Get_TfsServerUrl; }
            set { Set_TfsServerUrl = value; }
        }

        public void Start()
        {
            Start_Called = true;
        }

        public void Stop()
        {
            Stop_Called = true;
        }

        #endregion
    }
}