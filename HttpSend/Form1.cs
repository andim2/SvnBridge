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
using System.Threading;

namespace HttpSend
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        static Thread processor = null;
        static StringBuilder output = new StringBuilder();

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (processor != null)
            {
                processor.Abort();
            }
            processor = new Thread(Process);
            processor.Start(new object[] { txtServer.Text, txtPort.Text, txtInput.Text });
            Thread.Sleep(500);
            Refresh();
        }

        private void Process(object parameters)
        {
            string serverName = (string)(((object[])parameters)[0]);
            int port = int.Parse((string)(((object[])parameters)[1]));
            TcpClient server = new TcpClient(serverName, port);
            NetworkStream stream = server.GetStream();
            byte[] input = Encoding.Default.GetBytes((string)(((object[])parameters)[2]));
            stream.Write(input, 0, input.Length);

            byte[] buffer = new byte[5000];
            int count;
            output = new StringBuilder();
            while (stream.CanRead && (count = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                output.Append(Encoding.Default.GetString(buffer, 0, count));
            }
            stream.Close();
            server.Close();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            Refresh();
        }

        private void Refresh()
        {
            txtOutput.Text = output.ToString();
        }
    }
}
