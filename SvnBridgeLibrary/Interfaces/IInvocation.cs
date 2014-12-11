using System.Reflection;

namespace SvnBridge.Interfaces
{
    public interface IInvocation
    {
        object[] Arguments{ get; }
        void Proceed();
        MethodInfo Method { get;}
        object ReturnValue { get; set; }
    }
}