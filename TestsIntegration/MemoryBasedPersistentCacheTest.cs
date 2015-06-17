using System;
using System.Collections.Generic;
using System.IO;
using CodePlex.TfsLibrary.ObjectModel;
using SvnBridge.Cache;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using Xunit;

namespace IntegrationTests
{
    public class MemoryBasedPersistentCacheTest
	{
        protected MemoryBasedPersistentCache cache;

        public MemoryBasedPersistentCacheTest()
		{
            WebCache webCache = new WebCache();
            webCache.Clear();
			RequestCache.Init();
            cache = new MemoryBasedPersistentCache(webCache);
		}

        [Fact]
		public void IfItemDoesNotExists_WillReturnNull()
		{
			Assert.Null(cache.Get(Guid.NewGuid().ToString()));
		}

		[Fact]
		public void CanSetAndGetItems()
		{
			cache.Set("test", 15);
			Assert.Equal<object>(15, cache.Get("test").Value);
		}

		[Fact]
		public void CanOverwriteValues()
		{
			cache.Set("test", 15);
			Assert.Equal<object>(15, cache.Get("test").Value);
			cache.Set("test", "blah");
			Assert.Equal<object>("blah", cache.Get("test").Value);
		}

		[Fact]
		public void CanSetAndGetSourceItem()
		{
			cache.Set("test", new SourceItem());
			Assert.True(cache.Get("test").Value is SourceItem);
		}

		[Fact]
		public void WillReturnFalseIfDoesNotContainsItem()
		{
			Assert.False(cache.Contains("test"));
		}

		[Fact]
		public void WillReturnTrueIfDoesNotContainsItem()
		{
			cache.Set("test", 1);
			Assert.True(cache.Contains("test"));
		}

		[Fact]
		public void CanAddItems()
		{
			for (int i = 0; i < 15; i++)
			{
				cache.Add("list", "I#" + i);
			}

			SvnBridge.Cache.HashSet<string> list = (SvnBridge.Cache.HashSet<string>)cache.Get("list").Value;
			int j=0;
			foreach (string s in list)
			{
				Assert.Equal("I#" + j, s);
				j += 1;
			}
			Assert.Equal(15, j);
		}

		[Fact]
		public void CanAddItemsAndGetList()
		{
			for (int i = 0; i < 15; i++)
			{
				cache.Set("I#" + i, i);
				cache.Add("list", "I#" + i);
			}

			List<int> list = cache.GetList<int>("list");
			Assert.Equal(15, list.Count);
			for (int i = 0; i < 15; i++)
			{
				Assert.Equal(i, list[i]);
			}
		}
	}
}
