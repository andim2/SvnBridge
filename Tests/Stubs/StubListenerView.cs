using SvnBridge.Presenters;
using SvnBridge.Views;

namespace UnitTests
{
    public class StubListenerView : IListenerView
    {
        public string ListenerErrorMessage;
        public bool OnListenerError_Called;
        public bool OnListenerStarted_Called;
        public bool OnListenerStopped_Called;
        public ListenerViewPresenter Set_Presenter;
        public bool Show_Called;

        #region IListenerView Members

        public ListenerViewPresenter Presenter
        {
            set { Set_Presenter = value; }
        }

        public void OnListenerStarted()
        {
            OnListenerStarted_Called = true;
        }

        public void OnListenerStopped()
        {
            OnListenerStopped_Called = true;
        }

        public void OnListenerError(string message)
        {
            OnListenerError_Called = true;
            ListenerErrorMessage = message;
        }

        public void Show()
        {
            Show_Called = true;
        }

        #endregion
    }
}