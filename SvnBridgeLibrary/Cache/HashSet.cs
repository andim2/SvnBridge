using System;
using System.Collections;
using System.Collections.Generic;

namespace SvnBridge.Cache
{
	[Serializable]
    public class HashSet<T> : IEnumerable<T>
	{
		private readonly Dictionary<T, object> inner = new Dictionary<T, object>();

	    public int Count
	    {
            get
            {
                return inner.Count;
            }
	    }

	    #region IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<T>) this).GetEnumerator();
		}

		#endregion

		#region IEnumerable<T> Members

		public IEnumerator<T> GetEnumerator()
		{
			return inner.Keys.GetEnumerator();
		}

		#endregion

		public void Add(T item)
		{
			inner[item] = null;
		}
	}
}