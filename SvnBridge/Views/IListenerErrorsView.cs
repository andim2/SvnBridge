namespace SvnBridge.Views
{
	using Presenters;

	public interface IListenerErrorsView
	{
		ListenerViewPresenter Presenter { set; }
		
		void AddError(string title, string content);
		void Show();
		void Close();
	}
}