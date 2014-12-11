using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace SvnBridge.Views
{
	using Presenters;

	public partial class ErrorsView : Form, IListenerErrorsView
	{
		private ListenerViewPresenter presenter;

		public ErrorsView()
		{
			InitializeComponent();
			Closing += delegate(object sender, CancelEventArgs e)
			{
				Hide();
				e.Cancel = presenter.ShouldCloseErrorView == false;
			};
			foreach (Control control in GetControlsRecursive(this))
			{
				control.KeyUp += ErrorsView_KeyUp;
			}
		}

		public IEnumerable<Control> GetControlsRecursive(Control ctrl)
		{
			yield return ctrl;
			foreach (Control child in ctrl.Controls)
			{
				yield return child;
				foreach (Control control in GetControlsRecursive(child))
				{
					yield return control;
				}
			}
		}

		public ListenerViewPresenter Presenter
		{
			set { presenter = value; }
		}

		public void AddError(string title, string content)
		{
			if (InvokeRequired)
			{
				Invoke((Action)delegate
				{
					AddError(title, content);
				});
			}
			else
			{
				ListViewItem item = new ListViewItem(new string[]{ title, DateTime.Now.ToString() });
				item.Tag = content;
				lbErrors.Items.Add(item);
			}
		}

		public delegate void Action();

		private void lbErrors_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (lbErrors.SelectedItems.Count == 0)
				return;
			txtErrorDetails.Text = (string)lbErrors.SelectedItems[0].Tag;
		}

		private void ErrorsView_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
				Hide();
		}

		private void clearToolStripMenuItem_Click(object sender, EventArgs e)
		{
			lbErrors.Items.Clear();
			txtErrorDetails.Text = "";
		}
	}
}