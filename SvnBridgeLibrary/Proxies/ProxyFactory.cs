using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using SvnBridge.Interfaces;

namespace SvnBridge.Proxies
{
    public static class ProxyFactory
    {
        public static T Create<T>(T instance, params IInterceptor[] interceptors)
        {
            return (T) Create(typeof (T), instance, interceptors);
        }

        public static object Create(Type type, object instance, params IInterceptor[] interceptors)
        {
            return new RemotingProxy(type, interceptors, instance).GetTransparentProxy();
        }

		[DebuggerStepThrough]
        public class RemotingProxy : RealProxy
        {
            private readonly IInterceptor[] interceptors;
            private readonly object target;

            public RemotingProxy(Type classToProxy,
                                 IInterceptor[] interceptors,
                                 object target)
                : base(classToProxy)
            {
                this.interceptors = interceptors;
                this.target = target;
            }

            public override IMessage Invoke(IMessage msg)
            {
                IMethodCallMessage callMessage = msg as IMethodCallMessage;

                if (callMessage == null)
                {
                    return null;
                }
                RemotingInvocation invocation = new RemotingInvocation(this, interceptors, callMessage, target);
                invocation.Proceed();
                return ReturnValue(invocation.ReturnValue, invocation.Arguments, callMessage);
            }

            private static IMessage ReturnValue(object value, object[] outParams, IMethodCallMessage mcm)
            {
                return new ReturnMessage(value, outParams, outParams == null ? 0 : outParams.Length, mcm.LogicalCallContext, mcm);
            }
        }
    }
}