using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.PathParsing;
using SvnBridge.Views;

namespace SvnBridge.Presenters
{
	public class ListenerViewPresenter
	{
		private readonly IListenerErrorsView errorsView;
		private readonly Listener listener;
		private readonly IListenerView view;
		private bool closed;

		public ListenerViewPresenter(IListenerView view,
		                             IListenerErrorsView errorsView,
		                             Listener listener)
		{
			this.listener = listener;
			this.view = view;
			this.errorsView = errorsView;
			view.Presenter = this;
			errorsView.Presenter = this;

			listener.ListenError += OnListenError;
		}

		public int Port
		{
			get { return listener.Port; }
		}

		public bool ShouldCloseErrorView
		{
			get { return closed; }
		}

		private void OnListenError(object sender, ListenErrorEventArgs e)
		{
			errorsView.AddError(e.Exception.Message, e.Exception.ToString());
			view.OnListenerError(e.Exception.Message);
		}

		public void ChangeSettings(ISettingsView settingsView)
		{
			SettingsViewPresenter settingsViewPresenter = new SettingsViewPresenter(settingsView, Program.GetProxyInfo());
			settingsViewPresenter.Port = listener.Port;
			settingsViewPresenter.IgnoredUsedPort = listener.Port;
			settingsViewPresenter.Show();

			if ((!settingsViewPresenter.Canceled) && (SettingsHaveChanged(settingsViewPresenter)))
			{
				Program.SaveSettings(settingsViewPresenter.ProxyInformation, settingsViewPresenter.Port);
				ApplyNewSettings(settingsViewPresenter.ProxyInformation, settingsViewPresenter.Port);
			}
		}

		public void Show()
		{
			view.Show();
		}

		public void StartListener()
		{
			IPathParser parser = new PathParserServerAndProjectInPath(Container.Resolve<TfsUrlValidator>());
			listener.Start(parser);
			view.OnListenerStarted();
		}

		public void StopListener()
		{
			listener.Stop();
			view.OnListenerStopped();
		}

		private void ApplyNewSettings(ProxyInformation proxy, int port)
		{
			StopListener();
			listener.Port = port;
			Proxy.Set(proxy);
			StartListener();
		}

		private bool SettingsHaveChanged(SettingsViewPresenter presenter)
		{
			return presenter.Port != listener.Port ||
				   presenter.ProxyInformation != Program.GetProxyInfo();
		}

		public void ShowErrors()
		{
			errorsView.Show();
		}

		public void ViewClosed()
		{
			closed = true;
			errorsView.Close();
		}
	}
}
