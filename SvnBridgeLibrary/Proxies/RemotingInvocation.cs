using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;


namespace SvnBridge.Proxies
{
	[DebuggerStepThrough]
	public class RemotingInvocation : IInvocation
    {
        private readonly IMethodCallMessage message;
        private readonly object target;
        private object _returnValue;
        private readonly RealProxy realProxy;
        private readonly IInterceptor[] interceptors;
        private readonly object[] args;
        private int interceptorIndex = 0;

        public RemotingInvocation(RealProxy realProxy, IInterceptor[] interceptors, IMethodCallMessage message, object target)
        {
            this.message = message;
            this.target = target;
            this.realProxy = realProxy;
            this.interceptors = interceptors;
            this.args = (object[])this.message.Properties["__Args"];
        }


        public object[] Arguments
        {
            get { return args; }
        }

        public object GetArgumentValue(int index)
        {
            throw new NotSupportedException();
        }

        public MethodInfo GetConcreteMethod()
        {
            return (MethodInfo)message.MethodBase;
        }

        public MethodInfo Method
        {
            get { return GetConcreteMethod(); }
        }

        public void Proceed()
        {
            if (interceptorIndex < interceptors.Length)
            {
                IInterceptor interceptor = interceptors[interceptorIndex];
                interceptorIndex += 1;
                interceptor.Invoke(this);
                return;
            }

            try
            {
                ReturnValue = message.MethodBase.Invoke(target, Arguments);
            }
            catch (TargetInvocationException e)
            {
                Exception exception = e.InnerException;

                ExceptionHelper.PreserveStackTrace(exception);

                throw exception;
            }
        }

        public object Proxy
        {
            get { return realProxy.GetTransparentProxy(); }
        }

        public object ReturnValue
        {
            get { return _returnValue; }
            set { _returnValue = value; }
        }
    }
}