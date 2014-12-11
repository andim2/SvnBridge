using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.Proxies;

namespace SvnBridge.Infrastructure
{
    public class Container
    {
        private static Container container = new Container();

        public static void Reset()
        {
            container = new Container();
        }

        public static void Register(Type service, Type impl)
        {
            container.RegisterType(service, impl);
        }

        public static void Register(Type service, object impl)
        {
            container.RegisterType(service, impl);
        }

        public static T Resolve<T>()
        {
            return (T)container.ResolveType(typeof(T));
        }

        private delegate object Creator();

        private readonly Dictionary<Type, Creator> typeToCreator = new Dictionary<Type, Creator>();
        private readonly Dictionary<Type, bool> performedValidation = new Dictionary<Type, bool>();

        public void RegisterType(Type service, Type impl)
        {
            if (!typeToCreator.ContainsKey(service))
            {
                lock (typeToCreator)
                {
                    if (!typeToCreator.ContainsKey(service))
                    {
                        List<Type> interceptorTypes = GetInterceptorTypes(impl);
                        Creator creator = GetAutoCreator(service, impl, interceptorTypes);
                        typeToCreator.Add(service, creator);
                    }
                }
            }
        }

        public void RegisterType(Type service, object impl)
        {
            typeToCreator.Add(service, delegate() { return impl; });
        }

        public object ResolveType(Type type)
        {
            Creator creator;
            bool typeRegistered;
            lock (typeToCreator)
            {
                typeRegistered = typeToCreator.TryGetValue(type, out creator);
            }
            if (!typeRegistered)
            {
                if (type.IsInterface)
                {
                    throw new InvalidOperationException("No component registered for interface " + type);
                }
                RegisterType(type, type);
                lock (typeToCreator)
                {
                    creator = typeToCreator[type];
                }
            }
            return creator();
        }

        private Creator GetAutoCreator(Type service, Type impl, List<Type> interceptorTypes)
        {
            return delegate()
            {
                object instance = CreateInstance(impl);
                List<IInterceptor> interceptors = new List<IInterceptor>();
                foreach (Type interceptorType in interceptorTypes)
                {
                    interceptors.Add((IInterceptor)ResolveType(interceptorType));
                }
                if (interceptors.Count == 0)
                    return instance;
                return ProxyFactory.Create(service, instance, interceptors.ToArray());
            };
        }

        private object CreateInstance(Type type)
        {
            List<object> args = new List<object>();
            ConstructorInfo[] constructors = type.GetConstructors();
            if (constructors.Length != 0)
            {
                foreach (ParameterInfo info in constructors[0].GetParameters())
                {
                    try
                    {
                        if (Configuration.AppSettings(info.Name) != null)
                        {
                            args.Add(Configuration.AppSettings(info.Name));
                        }
                        else if (RequestCache.IsInitialized && RequestCache.Items.Contains(info.Name))
                        {
                            args.Add(RequestCache.Items[info.Name]);
                        }
                    	else
                    	{
                    		args.Add(ResolveType(info.ParameterType));
                    	}
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException(
                            "Failed trying to resolve constructor parameter '" + info.Name + "' for: " + type, e);
                    }
                }
            }
            if (args.Count > 0)
            {
                return Activator.CreateInstance(type, args.ToArray());
            }
            else
            {
                return Activator.CreateInstance(type);
            }
        }

        private List<Type> GetInterceptorTypes(Type impl)
        {
            List<Type> interceptors = new List<Type>();
            object[] attributes = impl.GetCustomAttributes(typeof(InterceptorAttribute), true);
            foreach (object attribute in attributes)
            {
                Type interceptorType = ((InterceptorAttribute)attribute).Interceptor;
                RegisterType(interceptorType, interceptorType);
                interceptors.Add(interceptorType);
            }
            return interceptors;
        }
    }
}