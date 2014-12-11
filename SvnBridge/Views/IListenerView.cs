using SvnBridge.Presenters;

namespace SvnBridge.Views
{
    public interface IListenerView
    {
        ListenerViewPresenter Presenter { set; }

        void OnListenerStarted();
        void OnListenerStopped();
        void OnListenerError(string message);
        void Show();
    }
}