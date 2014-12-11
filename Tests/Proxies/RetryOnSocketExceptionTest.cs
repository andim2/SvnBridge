using System;
using System.Net;
using System.Net.Sockets;
using Xunit;
using SvnBridge.Interfaces;
using SvnBridge.Proxies;
using Tests;
using SvnBridge.Infrastructure;

namespace UnitTests
{
    public class RetryOnSocketExceptionTest
    {
        private readonly MyMocks stubs;

        public RetryOnSocketExceptionTest()
        {
            stubs = new MyMocks();
        }

        [Fact]
        public void WillNotFailOnFirstSocketException()
        {
			RetryOnExceptionsInterceptor<SocketException> interceptor = new RetryOnExceptionsInterceptor<SocketException>(stubs.CreateObject<DefaultLogger>());
            StubInvocation mock = new StubInvocation();
            mock.Proceed_ReturnList.Add(new SocketException());
            mock.Proceed_ReturnList.Add(null);

            interceptor.Invoke(mock);

            Assert.Equal(2, mock.Proceed_CallCount);
        }

        [Fact]
        public void WillNotFailOnFirstWebException()
        {
            RetryOnExceptionsInterceptor<WebException> interceptor = new RetryOnExceptionsInterceptor<WebException>(stubs.CreateObject<DefaultLogger>());
            StubInvocation mock = new StubInvocation();
            mock.Proceed_ReturnList.Add(new WebException());
            mock.Proceed_ReturnList.Add(null);

            interceptor.Invoke(mock);

            Assert.Equal(2, mock.Proceed_CallCount);
        }

        [Fact]
        public void WillFailOnNonSocketOrWebException()
        {
            RetryOnExceptionsInterceptor<WebException> interceptor = new RetryOnExceptionsInterceptor<WebException>(stubs.CreateObject<DefaultLogger>());
            StubInvocation mock = new StubInvocation();
            mock.Proceed_ReturnList.Add(new InvalidOperationException());

            Exception result = Record.Exception(delegate { interceptor.Invoke(mock); });

            Assert.IsType(typeof(InvalidOperationException), result);
            Assert.Equal(1, mock.Proceed_CallCount);
        }

        [Fact]
        public void WillThrowAfterThreeAttempts()
        {
            RetryOnExceptionsInterceptor<SocketException> interceptor = new RetryOnExceptionsInterceptor<SocketException>(stubs.CreateObject<DefaultLogger>());
            StubInvocation mock = new StubInvocation();
            mock.Proceed_ReturnList.Add(new SocketException());
            mock.Proceed_ReturnList.Add(new SocketException());
            mock.Proceed_ReturnList.Add(new SocketException());

            Exception result = Record.Exception(delegate { interceptor.Invoke(mock); });

            Assert.IsType(typeof(SocketException), result);
            Assert.Equal(3, mock.Proceed_CallCount);
        }
    }
}
