using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Xunit.Sdk;

namespace TestsEndToEnd
{
	public class ConcurrentActionsTest : EndToEndTestBase
	{
		[SvnBridgeFact(Skip="Currently not working")]
		public void RunAllTestsConcurrentyly()
		{
			List<MethodInfo> tests = new List<MethodInfo>();
			Type[] types = Assembly.GetExecutingAssembly().GetTypes();
			foreach (Type type in types)
			{
				if (type.IsAbstract)
					continue;
				if (type == typeof(ConcurrentActionsTest))
					continue;
				foreach (MethodInfo info in type.GetMethods())
				{
					object[] attributes = info.GetCustomAttributes(typeof(SvnBridgeFactAttribute), true);
					if (attributes.Length == 0)
						continue;
					tests.Add(info);
				}
			}

			List<IAsyncResult> results = new List<IAsyncResult>();
			List<Exception> errors = new List<Exception>();
			ExecuteTestDelegate exec = ExecuteTest;
			foreach (MethodInfo test in tests)
			{
				IAsyncResult invoke = exec.BeginInvoke(test, new ConcurrentTestCommand(TestUrl, test), errors, null, null);
				results.Add(invoke);
			}
			foreach (IAsyncResult result in results)
			{
				result.AsyncWaitHandle.WaitOne();
				exec.EndInvoke(result);
			}
			if (errors.Count > 0)
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("Tests: " + results.Count + ", failed: " + errors.Count);
				foreach (Exception error in errors)
				{
					sb.AppendLine(error.ToString());
				}
				throw new Exception(sb.ToString());
			}
		}

		private delegate void ExecuteTestDelegate(MethodInfo test, ITestCommand command, List<Exception> errors);

		private void ExecuteTest(MethodInfo test, ITestCommand command, List<Exception> errors)
		{
			try
			{
				Console.WriteLine("Running: " + test);
				object instance = Activator.CreateInstance(test.DeclaringType);
				command.Execute(instance);
			}
			catch (TargetInvocationException e)
			{
				lock (errors)
					errors.Add(e.InnerException);
			}
			catch (Exception e)
			{
				lock (errors)
					errors.Add(e);
			}
		}
	}

	internal class ConcurrentTestCommand : ITestCommand
	{
		private string testUrl;
		private readonly ITestCommand testCommand;

		public ConcurrentTestCommand(string testUrl, MethodInfo method)
		{
			this.testUrl = testUrl;
			this.testCommand = new TestCommand(method);
		}

		#region ITestCommand Members

		public MethodResult Execute(object testClass)
		{
			((EndToEndTestBase) testClass).TestUrl = testUrl;
			return testCommand.Execute(testClass);
		}

		public string Name
		{
			get { return testCommand.Name; }
		}

		#endregion
	}
}
