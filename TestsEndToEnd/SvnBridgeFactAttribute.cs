using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using SvnBridge.Cache;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using SvnBridge.PathParsing;
using Xunit;
using Xunit.Sdk;

namespace EndToEndTests
{
	public class SvnBridgeFactAttribute : FactAttribute
	{
		protected override IEnumerable<ITestCommand> EnumerateTestCommands(IMethodInfo method)
		{
			if (Skip != null)
				yield return new SkipCommand(method, "", "");

			foreach (ITestCommand command in GetTestCommandsFromBase(method))
			{
			    using (new ConsoleColorer(ConsoleColor.Gray))
				{
					Debug.WriteLine("Test (UsingRequestBasePathParser): " + method);
				}
				yield return new UsingRequestBasePathParser(command);
			}
		}

		private IEnumerable<ITestCommand> GetTestCommandsFromBase(IMethodInfo method)
		{
			return base.EnumerateTestCommands(method);
		}
	}

	internal class UsingRequestBasePathParser : ITestCommand
	{
		private readonly ITestCommand command;

		public UsingRequestBasePathParser(ITestCommand command)
		{
			this.command = command;
		}

		public MethodResult Execute(object testClass)
		{
			var test = (EndToEndTestBase) testClass;
            test.TestRoot = false;
            test.Initialize();

		    var serverUrl = new Uri(test.ServerUrl).Host + ":" + new Uri(test.ServerUrl).Port;
            if (test.ServerUrl.Contains("/tfs"))
                serverUrl += new Uri(test.ServerUrl).PathAndQuery + "/$";

		    string testUrl = "http://" + IPAddress.Loopback + ":" + test.Port + "/" + serverUrl + "/SvnBridgeTesting" + test.TestPath;

			IPathParser parser = new PathParserServerAndProjectInPath(new TfsUrlValidator(new WebCache()));

			((EndToEndTestBase) testClass).Initialize(testUrl, parser);
			try
			{
				return command.Execute(testClass);
			}
			catch (Exception e)
			{
				using (new ConsoleColorer(ConsoleColor.Red))
				{
					Console.WriteLine("Failed: {0}", e.Message);
				}
				throw;
			}
		}

        public string DisplayName
        {
            get { return command.DisplayName; }
        }

        public bool ShouldCreateInstance
        {
            get { return command.ShouldCreateInstance; }
        }

        public System.Xml.XmlNode ToStartXml()
        {
            return command.ToStartXml();
        }
    }

	public class ConsoleColorer : IDisposable
	{
		public ConsoleColorer(ConsoleColor newColor)
		{
			this.oldColor = Console.ForegroundColor;
			Console.ForegroundColor = newColor;
		}

		private ConsoleColor oldColor;

		public void Dispose()
		{
			Console.ForegroundColor = oldColor;
		}
	}
}