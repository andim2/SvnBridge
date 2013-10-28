using System;
using System.Windows.Forms;
using SvnBridge.Presenters;
using SvnBridge.Utility;

namespace SvnBridge.Views
{
	public partial class SettingsForm : Form, ISettingsView
	{
		private SettingsViewPresenter presenter;
		private readonly ProxySettings proxySettings = new ProxySettings();

		public SettingsForm()
		{
			InitializeComponent();
		}

		#region ISettingsView Members

		public SettingsViewPresenter Presenter
		{
			set { presenter = value; }
		}

		public new void Show()
		{
			txtPortNumber.Text = presenter.Port.ToString();
			ShowDialog();
		}

		#endregion

		private void OnProxyButtonClicked(object sender, EventArgs e)
		{
			proxySettings.SetInformation(presenter.ProxyInformation);
			if (proxySettings.ShowDialog(this) != DialogResult.OK)
				return;
			presenter.ProxyInformation.UseProxy = string.IsNullOrEmpty(proxySettings.ProxyUrl) == false;
			presenter.ProxyInformation.Url = proxySettings.ProxyUrl;
			presenter.ProxyInformation.Port = proxySettings.Port;
			presenter.ProxyInformation.Username = proxySettings.Username;
			presenter.ProxyInformation.Password = proxySettings.Password;
			presenter.ProxyInformation.UseDefaultCredentails = proxySettings.UseDefaultCredentials;
            presenter.ProxyInformation.TfsProxyUrl = proxySettings.TfsProxyUrl;
			presenter.UpdatedProxyInformation();
		}

		private void OnOkButtonClicked(object sender,
									   EventArgs e)
		{
			if (!Helper.IsValidPort(txtPortNumber.Text))
			{
				MessageBox.Show(
					"The port number does not appear to be valid. Please choose a number between 1 and 65535.",
					"SvnBridge",
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				txtPortNumber.Focus();
				txtPortNumber.SelectAll();
				return;
			}

			int portNumber = int.Parse(txtPortNumber.Text);
			if (presenter.IgnoredUsedPort != portNumber && Helper.IsPortInUseOnLocalHost(portNumber))
			{
				MessageBox.Show(
					"The port number appears to already be in use. Please choose a different port.",
					"SvnBridge",
					MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				txtPortNumber.Focus();
				txtPortNumber.SelectAll();
				return;
			}

			presenter.Port = int.Parse(txtPortNumber.Text);
			DialogResult = DialogResult.OK;
			Close();
		}

		private void OnCancelButtonClicked(object sender,
										   EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}
	}
}
