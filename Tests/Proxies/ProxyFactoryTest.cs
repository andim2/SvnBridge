using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Xunit;
using SvnBridge.Interfaces;
using SvnBridge.Proxies;

namespace SvnBridge.Proxies.Tests
{
    public class ProxyFactoryTest
    {
        [Fact]
        public void CanInterceptMethodCall()
        {
            LoggingInterceptor interceptor = new LoggingInterceptor();
            IFoo foo = ProxyFactory.Create<IFoo>(new FooImpl(), interceptor);
            foo.Method1();
            Assert.Equal("Method1", interceptor.calls[0]);
        }

        [Fact]
        public void CanInterceptMethodCallAndThenProceed()
        {
            ErrorCountInterceptor interceptor = new ErrorCountInterceptor();
            IFoo foo = ProxyFactory.Create<IFoo>(new FooImpl(), interceptor);
            foo.Method1();
            Assert.Equal("SocketException", interceptor.errors[0].GetType().Name);
        }

        [Fact]
        public void CanModifyArgumentsToCall()
        {
            MultiplyByTwoInterceptor interceptor = new MultiplyByTwoInterceptor();
            IFoo foo = ProxyFactory.Create<IFoo>(new FooImpl(), interceptor);
            int actual = foo.Add(2, 3);
            Assert.Equal(10, actual);
        }

        [Fact]
        public void CanModifyReturnValue()
        {
            DivideReturnValueBy3Interceptor interceptor = new DivideReturnValueBy3Interceptor();
            IFoo foo = ProxyFactory.Create<IFoo>(new FooImpl(), interceptor);
            int actual = foo.Add(3, 3);
            Assert.Equal(2, actual);
        }

        [Fact]
        public void CanRegisterTwoInterceptors()
        {
            MultiplyByTwoInterceptor interceptor1 = new MultiplyByTwoInterceptor();
            DivideReturnValueBy3Interceptor interceptor2 = new DivideReturnValueBy3Interceptor();
            IFoo foo = ProxyFactory.Create<IFoo>(new FooImpl(), interceptor1, interceptor2);
            int actual = foo.Add(3, 3);
            Assert.Equal(4, actual);
        }
    }

    internal class DivideReturnValueBy3Interceptor : IInterceptor
    {
        public void Invoke(IInvocation invocation)
        {
            invocation.Proceed();
            invocation.ReturnValue = ((int) invocation.ReturnValue)/3;
        }
    }

    internal class MultiplyByTwoInterceptor : IInterceptor
    {
        public void Invoke(IInvocation invocation)
        {
            for (int i = 0; i < invocation.Arguments.Length; i++)
            {
                invocation.Arguments[i] = ((int) invocation.Arguments[i])*2;
            }
            invocation.Proceed();
        }
    }

    internal class ErrorCountInterceptor : IInterceptor
    {
        public List<Exception> errors = new List<Exception>();

        public void Invoke(IInvocation invocation)
        {
            try
            {
                invocation.Proceed();
            }
            catch (System.Exception e)
            {
                errors.Add(e);
            }
        }
    }

    internal class LoggingInterceptor : IInterceptor
    {
        public List<string> calls = new List<string>();
        public void Invoke(IInvocation invocation)
        {
            calls.Add(invocation.Method.Name);
        }
    }

    public interface IFoo
    {
        void Method1();
        int Add(int a, int b);
    }

    public class FooImpl : IFoo
    {
        public void Method1()
        {
            throw new SocketException();
        }

        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}