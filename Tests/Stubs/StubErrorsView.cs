namespace SvnBridge.Stubs
{
	using Presenters;
	using Views;

	public class StubErrorsView : IListenerErrorsView
	{
		public bool AddError_Called;
		public bool Show_Called;
		private ListenerViewPresenter presenter;
		public bool Closed_Called;

		public ListenerViewPresenter Presenter
		{
			set { presenter = value; }
		}

		public void AddError(string title, string content)
		{
			AddError_Called = true;
		}

		public void Show()
		{
			Show_Called = true;
		}

		public void Close()
		{
			Closed_Called = true;
		}
	}
}