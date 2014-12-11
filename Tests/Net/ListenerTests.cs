using System;
using SvnBridge.Infrastructure.Statistics;
using SvnBridge.Interfaces;
using SvnBridge.PathParsing;
using Xunit;
using SvnBridge.Infrastructure;
using Tests;
using SvnBridge.SourceControl;
using SvnBridge.Net;

namespace UnitTests
{
    public class ListenerTests
    {
        protected MyMocks stubs = new MyMocks();

        [Fact]
        public void SetPortAfterStartThrows()
        {
            Listener listener = new Listener(stubs.CreateObject<DefaultLogger>(), stubs.CreateObject<ActionTrackingViaPerfCounter>());
            listener.Port = 10011;
            listener.Start(new PathParserSingleServerWithProjectInPath("http://foo"));

            Assert.Throws<InvalidOperationException>(
                delegate { listener.Port = 8082; });

            listener.Stop();
        }

        [Fact]
        public void StartWithoutSettingPortThrows()
        {
            Listener listener = new Listener(stubs.CreateObject<DefaultLogger>(), stubs.CreateObject<ActionTrackingViaPerfCounter>());
            
            Assert.Throws<InvalidOperationException>(
				delegate { listener.Start(new PathParserSingleServerWithProjectInPath("http://foo")); });
        }
    }
}