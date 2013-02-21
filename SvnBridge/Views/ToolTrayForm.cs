using System;
using System.Diagnostics;
using System.Windows.Forms;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using SvnBridge.Presenters;
using SvnBridge.Cache;

namespace SvnBridge.Views
{
    public partial class ToolTrayForm : Form, IListenerView
    {
        private ListenerViewPresenter presenter;
    	private bool hasErrors = false;
        public ToolTrayForm()
        {
            InitializeComponent();
			notifyIcon.BalloonTipClicked+=BalloonTipClicked_OnClick;
			Closed+=((sender, e) => presenter.ViewClosed());
			showErrorsToolStripMenuItem.Click += OnShowErrorsClick;
            invokeDebugger.Click+=((sender, e) => Debugger.Launch());
        }

    	private void BalloonTipClicked_OnClick(object sender, EventArgs e)
    	{
			if (hasErrors==false)
				return;
    		presenter.ShowErrors();
    	}

    	#region IListenerView Members

        public void OnListenerStarted()
        {
            string serverAndPort = "http://localhost:" + presenter.Port;
            notifyIcon.Text = "Running: " + serverAndPort;
            startToolStripMenuItem.Enabled = false;
            stopToolStripMenuItem.Enabled = true;

        	string text = "Started on port " + presenter.Port + "\r\nForward by request url. Sample:\r\n" +
					serverAndPort + "/tfs03.codeplex.com/SvnBridge";

        	notifyIcon.ShowBalloonTip(500,
                                      "SvnBridge",
                                      text,
                                      ToolTipIcon.Info);
        }

        public void OnListenerStopped()
        {
            notifyIcon.BalloonTipText = "Not running";
            startToolStripMenuItem.Enabled = true;
            stopToolStripMenuItem.Enabled = false;

            notifyIcon.ShowBalloonTip(500, "SvnBridge", "Stopped", ToolTipIcon.Info);
        }

        public void OnListenerError(string message)
        {
        	hasErrors = true;
            notifyIcon.ShowBalloonTip(1000, "SvnBridge", message, ToolTipIcon.Error);
        }

        public ListenerViewPresenter Presenter
        {
            set { presenter = value; }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            presenter.StopListener();
        }

        private void OnExitClicked(object sender,
                                   EventArgs e)
        {
            Close();
        }

        protected override void OnShown(EventArgs e)
        {
            Hide();
        }

        private void OnSettingsClicked(object sender,
                                       EventArgs e)
        {
            SettingsForm settingsView = new SettingsForm();

            presenter.ChangeSettings(settingsView);
        }

		private void OnShowErrorsClick(object sender, EventArgs e)
		{
			presenter.ShowErrors();
		}

        private void OnStartClicked(object sender,
                                    EventArgs e)
        {
            presenter.StartListener();
        }

        private void OnStopClicked(object sender,
                                   EventArgs e)
        {
            presenter.StopListener();
        }

        public void Run()
        {
            presenter.StartListener();
        }
    }
}
