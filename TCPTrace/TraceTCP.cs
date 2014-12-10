using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Trace
{
    public partial class TraceTCP : Form
    {
        private static bool _keepListening = true;
        private static int _lastDirection = 0;
        private static TcpListener _server;
        private static string _targetPort;
        private static string _targetServer;
        private static int _testCount = 0;

        public TraceTCP()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender,
                                   EventArgs e)
        {
            txtPort.Enabled = false;
            txtTargetPort.Enabled = false;
            txtTargetServer.Enabled = false;
            button1.Enabled = false;

            File.WriteAllText("output1.txt", "");
            int port = int.Parse(txtPort.Text);
            _server = new TcpListener(IPAddress.Loopback, port);
            _targetServer = txtTargetServer.Text;
            _targetPort = txtTargetPort.Text;

            File.WriteAllText(@"Tests.txt", "");
            WriteTestLogLine("using System;");
            WriteTestLogLine("using SvnBridge.SourceControl;");
            WriteTestLogLine("using CodePlex.TfsLibrary;");
            WriteTestLogLine("using CodePlex.TfsLibrary.RepositoryWebSvc;");
            WriteTestLogLine("using Xunit;");
            WriteTestLogLine("using Attach;");
            WriteTestLogLine("using Tests;");

            WriteTestLogLine("");
            WriteTestLogLine("namespace ProtocolTests");
            WriteTestLogLine("{");
            WriteTestLogLine("    public class Tests : ProtocolTestsBase");
            WriteTestLogLine("    {");

            Thread requestProcessor = new Thread(StartListening);
            requestProcessor.IsBackground = true;
            requestProcessor.Start();
        }

        public static void WriteTestLog(string log)
        {
            bool retry = false;
            do
            {
                retry = false;
                try
                {
                    File.AppendAllText(@"Tests.txt", log);
                }
                catch
                {
                    Thread.Sleep(100);
                    retry = true;
                }
            } while (retry);
        }

        public static void WriteTestLogLine(string log)
        {
            WriteTestLog(log + "\r\n");
        }

        public static void StartListening()
        {
            List<Thread> threads = new List<Thread>();
            _server.Start();
            while (_keepListening)
            {
                TcpClient client = _server.AcceptTcpClient();
                Thread newConnection = new Thread(HandleConnection);
                newConnection.IsBackground = true;
                threads.Add(newConnection);
                newConnection.Start(new object[] {client, _targetServer, _targetPort});
            }
        }

        public static void HandleConnection(object parameters)
        {
            TcpClient client = (TcpClient) ((object[]) parameters)[0];
            string serverName = (string) ((object[]) parameters)[1];
            string port = (string) ((object[]) parameters)[2];
            NetworkStream clientStream = client.GetStream();

            TcpClient server = new TcpClient(serverName, int.Parse(port));
            NetworkStream serverStream = server.GetStream();
            CopyStreams(clientStream, serverStream);
            serverStream.Close();
            server.Close();

            clientStream.Close();
            client.Close();
        }

        public static void CopyStreams(NetworkStream input,
                                       NetworkStream output)
        {
            byte[] buffer = new byte[5000];
            Thread copyInput = new Thread(CopyStream);
            copyInput.IsBackground = true;
            Thread copyOutput = new Thread(CopyStream);
            copyOutput.IsBackground = true;

            copyInput.Start(new object[] {input, output, 1});
            copyOutput.Start(new object[] {output, input, 2});

            copyInput.Join();
            copyOutput.Join();
        }

        public static void WriteLog(byte[] buffer,
                                    int count)
        {
            bool retry;
            do
            {
                retry = false;
                try
                {
                    //string data = Encoding.UTF8.GetString(buffer, 0, count);
                    using (FileStream stream = File.OpenWrite("output1.txt"))
                    {
                        stream.Position = stream.Length;
                        //byte[] start = Encoding.UTF8.GetBytes("++");
                        //stream.Write(start, 0, start.Length);
                        stream.Write(buffer, 0, count);
                        //byte[] end = Encoding.UTF8.GetBytes("--");
                        //stream.Write(end, 0, end.Length);
                    }
                    //System.Diagnostics.Debug.Write(data);
                }
                catch
                {
                    retry = true;
                    Thread.Sleep(100);
                }
            } while (retry);
        }

        public static void CopyStream(object parameters)
        {
            Stream input = (Stream) ((object[]) parameters)[0];
            Stream output = (Stream) ((object[]) parameters)[1];
            int direction = (int) ((object[]) parameters)[2];
            byte[] buffer = new byte[5000];
            int count;
            try
            {
                while (input.CanRead && (count = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    //string data = Encoding.Default.GetString(buffer, 0, count);
                    //if (data.StartsWith("PUT //"))
                    //    System.Threading.Thread.Sleep(500);

                    WriteTest(buffer, count, direction);
                    WriteLog(buffer, count);
                    output.Write(buffer, 0, count);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Failed to copy stream: " + e.Message);
            }
        }

        public static void WriteTest(byte[] buffer,
                                     int count,
                                     int direction)
        {
            StringBuilder output = new StringBuilder();
            if (_lastDirection == 0)
            {
                output.AppendLine("        [Fact]");
                _testCount++;
                output.AppendLine("        public void Test" + _testCount.ToString() + "()");
                output.AppendLine("        {");
                output.AppendLine("            string request =");
            }
            else
            {
                if (_lastDirection == direction)
                {
                    output.AppendLine("\" +");
                }
                else
                {
                    output.AppendLine("\";");
                    if (direction == 1)
                    {
                        output.AppendLine("");
                        output.AppendLine("            string actual = ProcessRequest(request, ref expected);");
                        output.AppendLine("");
                        output.AppendLine("            Assert.Equal(expected, actual);");
                        output.AppendLine("        }");
                        output.AppendLine("");
                        output.AppendLine("        [Fact]");
                        _testCount++;
                        output.AppendLine("        public void Test" + _testCount.ToString() + "()");
                        output.AppendLine("        {");
                        output.AppendLine("            string request =");
                    }
                    else
                    {
                        output.AppendLine("");
                        output.AppendLine("            string expected =");
                    }
                }
            }
            output.Append("                \"");
            for (int i = 0; i < count; i++)
            {
                if (buffer[i] == 0)
                {
                    output.Append("\\0");
                }
                else if (buffer[i] == 10)
                {
                    output.Append("\\n");
                    if (i + 1 < count)
                    {
                        output.AppendLine("\" +");
                        output.Append("                \"");
                    }
                }
                else if (buffer[i] == 13)
                {
                    output.Append("\\r");
                }
                else if (buffer[i] == 34)
                {
                    output.Append("\\\"");
                }
                else if (buffer[i] == 34)
                {
                    output.Append("\\\"");
                }
                else if (buffer[i] == 92)
                {
                    output.Append("\\\\");
                }
                else if (buffer[i] < 32 || buffer[i] > 126)
                {
                    output.Append("\\u00" + string.Format("{0:X2}", buffer[i]));
                }
                else
                {
                    output.Append(Encoding.UTF8.GetString(buffer, i, 1));
                }
            }
            WriteTestLog(output.ToString());
            _lastDirection = direction;
        }

		private void txtPort_TextChanged(object sender, EventArgs e)
		{

		}

		private void label1_Click(object sender, EventArgs e)
		{

		}
    }
}