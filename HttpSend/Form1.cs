using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.IO;

namespace HttpSend
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            TcpClient server = new TcpClient(txtServer.Text, int.Parse(txtPort.Text));
            NetworkStream stream = server.GetStream();
            byte[] input = Encoding.Default.GetBytes(txtInput.Text);
            stream.Write(input, 0, input.Length);

            byte[] buffer = new byte[5000];
            int count;
            StringBuilder output = new StringBuilder();
            count = stream.Read(buffer, 0, buffer.Length);
            {
                output.Append(Encoding.Default.GetString(buffer, 0, count));
                txtOutput.Text = output.ToString();
            }
            stream.Close();
            server.Close();
        }
    }
}
