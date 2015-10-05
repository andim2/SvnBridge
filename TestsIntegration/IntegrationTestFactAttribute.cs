using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Xunit.Sdk;
using System.Reflection;

namespace IntegrationTests
{
    public class IntegrationTestFactAttribute : FactAttribute
    {
        protected override IEnumerable<ITestCommand> EnumerateTestCommands(IMethodInfo method)
        {
            if (Skip != null)
            {
                yield return new SkipCommand(method, "", "");
            }
            else
            {
                foreach (ITestCommand command in GetTestCommandsFromBase(method))
                {
                    yield return new UsingSubFolder(command);
                    yield return new UsingRootFolder(command);
                }
            }
        }

        private IEnumerable<ITestCommand> GetTestCommandsFromBase(IMethodInfo method)
        {
            return base.EnumerateTestCommands(method);
        }
    }

    internal class UsingSubFolder : ITestCommand
    {
		private readonly ITestCommand command;

		public UsingSubFolder(ITestCommand command)
		{
			this.command = command;
		}

        public MethodResult Execute(object testClass)
        {
            var test = (TFSSourceControlProviderTestsBase)testClass;
            test.TestRoot = false;
            test.Initialize();
            return command.Execute(testClass);
        }

        public string DisplayName
        {
            get { return command.DisplayName; }
        }

        public bool ShouldCreateInstance
        {
            get { return command.ShouldCreateInstance; }
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
            return command.ToStartXml();
        }
    }

    internal class UsingRootFolder : ITestCommand
    {
		private readonly ITestCommand command;

		public UsingRootFolder(ITestCommand command)
		{
			this.command = command;
		}

        public MethodResult Execute(object testClass)
        {
            var test = (TFSSourceControlProviderTestsBase)testClass;
            test.TestRoot = true;
            test.Initialize();
            return command.Execute(testClass);
        }

        public string DisplayName
        {
            get { return command.DisplayName + "AtRoot"; }
        }

        public bool ShouldCreateInstance
        {
            get { return command.ShouldCreateInstance; }
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
            return command.ToStartXml();
        }
    }
}
