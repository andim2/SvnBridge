using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Xunit.Sdk;

namespace EndToEndTests
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
            this.testCommand = ConstructITestCommand(method);
		}

        /// <summary>
        /// Almost comment-only helper.
        /// </summary>
        /// <remarks>
        /// Using TestCommand this is not compilable any more
        /// on newer xUnit (xunit.net_1.9.2-build-1705,
        /// which thus seems to be xUnit protocol v2 rather than v1 -
        /// previous version is 1.4.9.1446),
        /// probably due to design correction
        /// (TestCommand::Execute() has been made abstract).
        /// Given that the related test class ConcurrentActionsTest
        /// references SvnBridgeFactAttribute (FactAttribute),
        /// I'd want to guess
        /// that FactCommand is the proper replacement here.
        /// Hmm, well, nope, FactCommand is not available in *older* xUnit.
        /// And since conditional compile (compile-time evaluation)
        /// branching on available interfaces or assembly versions
        /// seems to be non-existent / very complicated,
        /// we'll have to keep using the *older* API.
        /// :(
        /// </remarks>
        private ITestCommand ConstructITestCommand(MethodInfo method)
        {
            return new TestCommand(Xunit.Sdk.Reflector.Wrap(method));
            //return new FactCommand(Xunit.Sdk.Reflector.Wrap(method));
        }

		public MethodResult Execute(object testClass)
		{
			((EndToEndTestBase) testClass).TestUrl = testUrl;
			return testCommand.Execute(testClass);
		}

        public string DisplayName
        {
            get { return testCommand.DisplayName; }
        }

        public bool ShouldCreateInstance
        {
            get { throw new NotImplementedException(); }
        }

        public int Timeout
        {
            get
            {
                return 0; // 0 == no timeout active; would we want this?
            }
        }

        public System.Xml.XmlNode ToStartXml()
        {
            throw new NotImplementedException();
        }
    }
}
