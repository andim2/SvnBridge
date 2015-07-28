using System;

namespace SvnBridge.Infrastructure
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class InterceptorAttribute : Attribute
    {
        public InterceptorAttribute(Type interceptor)
        {
            Interceptor = interceptor;
        }

        public Type Interceptor;
    }
}
