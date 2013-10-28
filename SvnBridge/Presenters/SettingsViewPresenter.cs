using System.Windows.Forms;
using SvnBridge.Net;
using SvnBridge.Views;

namespace SvnBridge.Presenters
{
	public class SettingsViewPresenter
	{
		private readonly ProxyInformation proxyInformation;
		private readonly ISettingsView view;
		private int port;
		private int ignoredUsedPort;

		public ProxyInformation ProxyInformation
		{
			get { return proxyInformation; }
		}

		public SettingsViewPresenter(ISettingsView view, ProxyInformation proxyInformation)
		{
			this.view = view;
			this.proxyInformation = proxyInformation;

			view.Presenter = this;
		}

		public bool Canceled
		{
			get
			{
				if (view.DialogResult == DialogResult.Cancel)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
		}

		public int Port
		{
			get { return port; }
			set { port = value; }
		}

		public int IgnoredUsedPort
		{
			get { return ignoredUsedPort; }
			set { ignoredUsedPort = value; }
		}

		public void Show()
		{
			view.Show();
		}

		public void UpdatedProxyInformation()
		{
			
		}
	}
}
