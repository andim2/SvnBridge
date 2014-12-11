namespace SvnBridge.Views
{
    partial class ProxySettings
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.proxyUrlTxtBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.portTxtBox = new System.Windows.Forms.TextBox();
            this.useDefaultCredetialsCheckBox = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.usernameTxtBox = new System.Windows.Forms.TextBox();
            this.passwordLabel = new System.Windows.Forms.Label();
            this.passwordTxtBox = new System.Windows.Forms.TextBox();
            this.cancelButton = new System.Windows.Forms.Button();
            this.okButton = new System.Windows.Forms.Button();
            this.tfsProxyUrlTxtBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 36);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(59, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Http &Proxy:";
            // 
            // proxyUrlTxtBox
            // 
            this.proxyUrlTxtBox.Location = new System.Drawing.Point(79, 36);
            this.proxyUrlTxtBox.Name = "proxyUrlTxtBox";
            this.proxyUrlTxtBox.Size = new System.Drawing.Size(218, 20);
            this.proxyUrlTxtBox.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 62);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(29, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "P&ort:";
            // 
            // portTxtBox
            // 
            this.portTxtBox.Location = new System.Drawing.Point(79, 62);
            this.portTxtBox.Name = "portTxtBox";
            this.portTxtBox.Size = new System.Drawing.Size(65, 20);
            this.portTxtBox.TabIndex = 5;
            // 
            // useDefaultCredetialsCheckBox
            // 
            this.useDefaultCredetialsCheckBox.AutoSize = true;
            this.useDefaultCredetialsCheckBox.Checked = true;
            this.useDefaultCredetialsCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.useDefaultCredetialsCheckBox.Location = new System.Drawing.Point(16, 87);
            this.useDefaultCredetialsCheckBox.Name = "useDefaultCredetialsCheckBox";
            this.useDefaultCredetialsCheckBox.Size = new System.Drawing.Size(137, 17);
            this.useDefaultCredetialsCheckBox.TabIndex = 6;
            this.useDefaultCredetialsCheckBox.Text = "Use Default Credentials";
            this.useDefaultCredetialsCheckBox.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(9, 118);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(58, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Username:";
            // 
            // usernameTxtBox
            // 
            this.usernameTxtBox.Enabled = false;
            this.usernameTxtBox.Location = new System.Drawing.Point(76, 113);
            this.usernameTxtBox.Name = "usernameTxtBox";
            this.usernameTxtBox.Size = new System.Drawing.Size(216, 20);
            this.usernameTxtBox.TabIndex = 8;
            // 
            // passwordLabel
            // 
            this.passwordLabel.AutoSize = true;
            this.passwordLabel.Location = new System.Drawing.Point(13, 144);
            this.passwordLabel.Name = "passwordLabel";
            this.passwordLabel.Size = new System.Drawing.Size(56, 13);
            this.passwordLabel.TabIndex = 9;
            this.passwordLabel.Text = "Password:";
            this.passwordLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // passwordTxtBox
            // 
            this.passwordTxtBox.Enabled = false;
            this.passwordTxtBox.Location = new System.Drawing.Point(75, 144);
            this.passwordTxtBox.Name = "passwordTxtBox";
            this.passwordTxtBox.PasswordChar = '*';
            this.passwordTxtBox.Size = new System.Drawing.Size(217, 20);
            this.passwordTxtBox.TabIndex = 10;
            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(215, 170);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 12;
            this.cancelButton.Text = "&Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // okButton
            // 
            this.okButton.Location = new System.Drawing.Point(132, 170);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 11;
            this.okButton.Text = "&Ok";
            this.okButton.UseVisualStyleBackColor = true;
            // 
            // tfsProxyUrlTxtBox
            // 
            this.tfsProxyUrlTxtBox.Location = new System.Drawing.Point(79, 9);
            this.tfsProxyUrlTxtBox.Name = "tfsProxyUrlTxtBox";
            this.tfsProxyUrlTxtBox.Size = new System.Drawing.Size(218, 20);
            this.tfsProxyUrlTxtBox.TabIndex = 1;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(13, 9);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(54, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "&Tfs Proxy:";
            // 
            // ProxySettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(303, 204);
            this.Controls.Add(this.tfsProxyUrlTxtBox);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.passwordTxtBox);
            this.Controls.Add(this.passwordLabel);
            this.Controls.Add(this.usernameTxtBox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.useDefaultCredetialsCheckBox);
            this.Controls.Add(this.portTxtBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.proxyUrlTxtBox);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ProxySettings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "ProxySettings";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox proxyUrlTxtBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox portTxtBox;
        private System.Windows.Forms.CheckBox useDefaultCredetialsCheckBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox usernameTxtBox;
        private System.Windows.Forms.Label passwordLabel;
        private System.Windows.Forms.TextBox passwordTxtBox;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.TextBox tfsProxyUrlTxtBox;
        private System.Windows.Forms.Label label4;
    }
}