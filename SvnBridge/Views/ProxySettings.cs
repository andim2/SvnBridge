using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SvnBridge.Net;
using SvnBridge.Utility;

namespace SvnBridge.Views
{
    public partial class ProxySettings : Form
    {
        public string TfsProxyUrl
        {
            get { return tfsProxyUrlTxtBox.Text; }
        }

        public string ProxyUrl
        {
            get { return proxyUrlTxtBox.Text;  }
        }

        public string Username
        {
            get { return usernameTxtBox.Text;  }
        }

        public string Password
        {
            get { return passwordTxtBox.Text;  }
        }

        public bool UseDefaultCredentials
        {
            get { return useDefaultCredetialsCheckBox.Checked; }
        }

        public int Port
        {
            get { return int.Parse(portTxtBox.Text);  }
        }

        public ProxySettings()
        {
            InitializeComponent();

            useDefaultCredetialsCheckBox.CheckedChanged += delegate
            {
                bool needManualCredentials = useDefaultCredetialsCheckBox.Checked == false;
                usernameTxtBox.Enabled = passwordTxtBox.Enabled = needManualCredentials;
                if(needManualCredentials==false)
                {
                    usernameTxtBox.Text = "";
                    passwordTxtBox.Text = "";
                }
            };

            okButton.Click += OnOkButtonClick;
            cancelButton.Click += OnCancelButtonClicked;
        }

        private void OnOkButtonClick(object sender, EventArgs e)
        {
            if (Helper.IsValidPort(portTxtBox.Text) == false)
            {
                MessageBox.Show(
                    "The port number does not appear to be valid. Please choose a number between 1 and 65535.",
                    "SvnBridge",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                portTxtBox.Focus();
                portTxtBox.SelectAll();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }


        private void OnCancelButtonClicked(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        public void SetInformation(ProxyInformation information)
        {
            portTxtBox.Text = information.Port.ToString();
            usernameTxtBox.Text = information.Username;
            useDefaultCredetialsCheckBox.Checked = information.UseDefaultCredentails;
            passwordTxtBox.Text = information.Password;
            proxyUrlTxtBox.Text = information.Url;
            tfsProxyUrlTxtBox.Text = information.TfsProxyUrl;
        }
    }
}