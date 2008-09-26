using System;
using System.Collections.Generic;
using System.Text;
using SvnBridge.Interfaces;

namespace SvnBridge.Stubs
{
    public class StubInvocation : IInvocation
    {
        public List<object> Proceed_ReturnList = new List<object>();
        public int Proceed_CallCount;

        public object[] Arguments
        {
            get { throw new NotImplementedException(); }
        }

        public void Proceed()
        {
            Proceed_CallCount++;
            if (Proceed_ReturnList[Proceed_CallCount - 1] != null)
                throw (Exception)Proceed_ReturnList[Proceed_CallCount - 1];
        }

        public System.Reflection.MethodInfo Method
        {
            get { throw new NotImplementedException(); }
        }

        public object ReturnValue
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
    }
}
