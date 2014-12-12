using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using SvnBridge.Interfaces;
using SvnBridge.Net;

namespace SvnBridge.Cache
{
	public class RegistryBasedPersistentCache : IPersistentCache
	{
		private const string NullToken = "NullToken:{B551C35D-95BC-489e-A34C-37D33759D239}";
		private RegistryKey registryKey;

		private static int UnitOfWorkNestingLevel
		{
			get
			{
				object item = PerRequest.Items["persistent.file.cache.current.UnitOfWorkNestingLevel"];
				if (item == null)
					return 0;
				return (int)item;
			}
			set { PerRequest.Items["persistent.file.cache.current.UnitOfWorkNestingLevel"] = value; }
		}

		private static IDictionary<string, PersistentItem> CurrentItems
		{
			get { return (IDictionary<string, PersistentItem>)PerRequest.Items["persistent.file.cache.current.items"]; }
			set { PerRequest.Items["persistent.file.cache.current.items"] = value; }
		}

		private static IDictionary<string, RegistryKey> CurrentKeys
		{
			get { return (IDictionary<string, RegistryKey>)PerRequest.Items["persistent.file.cache.current.keys"]; }
			set { PerRequest.Items["persistent.file.cache.current.keys"] = value; }
		}

		#region ICanValidateMyEnvironment Members

		public void ValidateEnvironment()
		{
			registryKey = Registry.CurrentUser.CreateSubKey("SvnBridge");
		}

		#endregion

		#region IPersistentCache Members

		public CachedResult Get(string key)
		{
			CachedResult result = null;
			UnitOfWork(delegate
			{
				if (CurrentItems.ContainsKey(key))
				{
					result = new CachedResult(CurrentItems[key].Item);
					return;
				}
				AddToCurrentUnitOfWork(key);
				PersistentItem deserialized = GetDeserializedObject(key);
				if (deserialized == null)
					return;
				CurrentItems[key] = deserialized;
				result = new CachedResult(deserialized.Item);
			});
			return result;
		}

		public void Set(string key, object obj)
		{
			AddToCurrentUnitOfWork(key);
			CurrentItems[key] = new PersistentItem(key, obj);
		}

		public bool Contains(string key)
		{
			bool contains = false;
			UnitOfWork(delegate
			{
				if (CurrentItems.ContainsKey(key))
				{
					contains = true;
					return;
				}
				using (RegistryKey item = registryKey.OpenSubKey(Hash(key)))
					contains = item != null;
			});
			return contains;
		}

		private static string Hash(string key)
		{
			if (key.Length > 255)
			{
				byte[] hash = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(key));
				return "@hashed-" + Convert.ToBase64String(hash);
			}
			return key;
		}

		public void Clear()
		{
			string[] names = registryKey.GetSubKeyNames();
			foreach (string subkey in names)
			{
				registryKey.DeleteSubKey(subkey);
			}
		}

		public void Add(string key, string value)
		{
			AddToCurrentUnitOfWork(key);
			value = value ?? NullToken;
			string hashedValue = Hash(value);
			CurrentKeys[Hash(key)].SetValue(hashedValue, value, RegistryValueKind.String);
		}

		public List<T> GetList<T>(string key)
		{
			List<T> items = new List<T>();
			UnitOfWork(delegate
			{
				CachedResult result = Get(key);
				if (result != null)
				{
					items.Add((T)result.Value);
					return;
				}

				RegistryKey cacheKey = CurrentKeys[Hash(key)];
				foreach (string itemKey in cacheKey.GetValueNames())
				{
					string realKey = itemKey;
					if (realKey == NullToken)
						continue;
					if (realKey.StartsWith("@hashed-"))
						realKey = (string)cacheKey.GetValue(itemKey);
					CachedResult itemResult = Get(realKey);
					if (itemResult != null)
						items.Add((T)itemResult.Value);
				}
			});
			return items;
		}

		public void UnitOfWork(Action action)
		{
			UnitOfWorkNestingLevel += 1;
			if (UnitOfWorkNestingLevel == 1)
			{
				CurrentItems = new Dictionary<string, PersistentItem>(StringComparer.InvariantCultureIgnoreCase);
				CurrentKeys = new Dictionary<string, RegistryKey>(StringComparer.InvariantCultureIgnoreCase);
			}
			try
			{
				action();

				if (UnitOfWorkNestingLevel == 1)
				{
					BinaryFormatter bf = new BinaryFormatter();
					foreach (PersistentItem item in CurrentItems.Values)
					{
						if (item.Changed == false)
							continue;
						using (MemoryStream ms = new MemoryStream())
						{
							bf.Serialize(ms, item);
							CurrentKeys[Hash(item.Name)]
								.SetValue("SerializedObject", ms.ToArray(), RegistryValueKind.Binary);
						}
					}
				}
			}
			finally
			{
				if (UnitOfWorkNestingLevel == 1)
				{
					foreach (RegistryKey key in CurrentKeys.Values)
					{
						key.Close();
					}
					CurrentItems = null;
				}
				UnitOfWorkNestingLevel -= 1;
			}
		}

		#endregion


		private PersistentItem GetDeserializedObject(string key)
		{
			using (RegistryKey cacheKey = registryKey.OpenSubKey(Hash(key)))
			{
				if (cacheKey == null)
					return null;
				BinaryFormatter formatter = new BinaryFormatter();
				byte[] buffer = (byte[])cacheKey.GetValue("SerializedObject");
				if (buffer == null)
					return null;
				using (MemoryStream stream = new MemoryStream(buffer))
				{
					PersistentItem deserialize = (PersistentItem)formatter.Deserialize(stream);
					if (deserialize != null &&
						string.Equals(key, deserialize.Name, StringComparison.InvariantCultureIgnoreCase) == false)
						return null;
					return deserialize;
				}
			}
		}

		protected virtual void AddToCurrentUnitOfWork(string key)
		{
			string hashedKey = Hash(key);
			if (CurrentKeys.ContainsKey(hashedKey))
				return;
			CurrentKeys[hashedKey] = registryKey.CreateSubKey(hashedKey);
		}

		#region Nested type: PersistentItem

		[Serializable]
		public class PersistentItem
		{
			[NonSerialized]
			public bool Changed;
			public object Item;
			public string Name;

			public PersistentItem()
			{
			}


			public PersistentItem(string name, object item)
			{
				Name = name;
				Item = item;
				Changed = true;
			}
		}

		#endregion
	}
}
