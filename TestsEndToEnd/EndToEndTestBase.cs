using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using IntegrationTests;
using SvnBridge;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using SvnBridge.Net;

namespace TestsEndToEnd
{
	public abstract class EndToEndTestBase : TFSSourceControlProviderTestsBase
	{
		#region Setup/Teardown
		private readonly string originalCurrentDirectory;

		protected EndToEndTestBase()
		{
			authenticateAsLowPrivilegeUser = new AuthenticateAsLowPrivilegeUser();
			port = new Random().Next(1024, short.MaxValue);
			originalCurrentDirectory = Environment.CurrentDirectory;
		}

		public string TestUrl
		{
			get { return testUrl; }
			set { testUrl = value; }
		}

		public virtual void Initialize(string url, IPathParser parser)
		{
			initialized = true;
			testUrl = url;

            BootStrapper.Start();

			CreateTempFolder();

			Environment.CurrentDirectory = Path.Combine(Path.GetTempPath(), checkoutFolder);
			Console.WriteLine("cd " + checkoutFolder);
			listener = Container.Resolve<Listener>();
			listener.ListenError += ((sender, e) => Console.WriteLine(e.Exception));
			listener.Port = port;

			listener.Start(parser);
		}

		private void CreateTempFolder()
		{
			checkoutFolder = Path.GetTempFileName();
			File.Delete(checkoutFolder);
			Directory.CreateDirectory(checkoutFolder);
			Console.WriteLine("md " + checkoutFolder);
		}

		public override void Dispose()
		{
			Environment.CurrentDirectory = originalCurrentDirectory;

			if (initialized == false)
				return;

			listener.Stop();

			base.Dispose();
			ForAllFilesInCurrentDirectory(
				delegate(FileInfo file)
				{
					try
					{
						file.Attributes = file.Attributes & ~FileAttributes.ReadOnly;
					}
					catch
					{
						// nothing much to do here
					}
				});

			authenticateAsLowPrivilegeUser.Dispose();
		}

		#endregion

		protected Listener listener;
		private string checkoutFolder;
		protected string testUrl;
		protected int port;
		private readonly AuthenticateAsLowPrivilegeUser authenticateAsLowPrivilegeUser;
		private bool initialized;

		protected static void ForAllFilesInCurrentDirectory(Action<FileInfo> action)
		{
			ForAllFilesIn(Environment.CurrentDirectory, action);
		}

		protected static void ForAllFilesIn(string directory,
										  Action<FileInfo> action)
		{
			foreach (string file in Directory.GetFiles(directory))
			{
				action(new FileInfo(file));
			}
			foreach (string dir in Directory.GetDirectories(directory))
			{
				ForAllFilesIn(dir, action);
			}
		}

		protected void CheckoutAndChangeDirectory()
		{
			Svn("co " + testUrl);
			Environment.CurrentDirectory =
				Path.Combine(Environment.CurrentDirectory, testPath.Substring(1) /* remove '/' */);
		}


		protected void CheckoutAgainAndChangeDirectory()
		{
			CreateTempFolder();
			Environment.CurrentDirectory = checkoutFolder;
			Console.WriteLine("cd " + checkoutFolder);
			Svn("co " + testUrl);
			Environment.CurrentDirectory =
				Path.Combine(Environment.CurrentDirectory, testPath.Substring(1) /* remove '/' */);
			Console.WriteLine("cd " + Environment.CurrentDirectory);
		}

		protected static string SvnExpectError(string command)
		{
			string err = null;
			ExecuteInternal(command, delegate(Process svn)
			{
				err = svn.StandardError.ReadToEnd();
			});
			Console.WriteLine(err);
			return err;
		}

		protected static string Svn(string command)
		{
			var output = new StringBuilder();
			var err = new StringBuilder();
			var readFromStdError = new Thread(prc =>
			{
				string line;
				while ((line = ((Process)prc).StandardError.ReadLine()) != null)
				{
					Console.WriteLine(line);
					err.AppendLine(line);
				}
			});
			var readFromStdOut = new Thread(prc =>
			{
				string line;
				while ((line = ((Process) prc).StandardOutput.ReadLine()) != null)
				{
					Console.WriteLine(line);
					output.AppendLine(line);
				}
			});
			ExecuteInternal(command, svn =>
			{
				readFromStdError.Start(svn);
				readFromStdOut.Start(svn);
			});

			readFromStdError.Join();
			readFromStdOut.Join();

			if (err.Length!=0)
			{
				throw new InvalidOperationException("Failed to execute command: " + err);
			}
			return output.ToString();
		}

		protected XmlDocument SvnXml(string command)
		{
			var document = new XmlDocument();
			document.LoadXml(Svn(command));
			return document;
		}

		private static void ExecuteInternal(string command, Action<Process> process)
		{
			Console.WriteLine("svn " + command);
			var psi = new ProcessStartInfo("svn", command)
									{
										RedirectStandardOutput = true,
										RedirectStandardError = true,
										CreateNoWindow = true,
										UseShellExecute = false
									};
			Process svn = Process.Start(psi);
			process(svn);
			svn.WaitForExit();
			svn.StandardError.BaseStream.Flush();
			svn.StandardOutput.BaseStream.Flush();
		}

		public int Port
		{
			get { return port; }
		}

		public string TestPath
		{
			get { return testPath; }
		}
	}
}
