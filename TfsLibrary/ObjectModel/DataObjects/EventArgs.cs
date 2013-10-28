using System;

namespace CodePlex.TfsLibrary.ObjectModel
{
    public class EventArgs<T> : EventArgs
    {
        T data;

        public EventArgs(T data)
        {
            this.data = data;
        }

        public T Data
        {
            get { return data; }
            set { data = value; }
        }
    }
}