using IntegrationTests.Properties;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge.Cache;

namespace IntegrationTests
{
	public class TfsUrlValidatorTest
	{
		[Fact]
		public void WillValidateHttpsUrl()
		{
			bool validUrl = new TfsUrlValidator(new WebCache()).IsValidTfsServerUrl("https://tfs03.codeplex.com");
			Assert.True(validUrl);
		}

		[Fact]
		public void WillRejectHttpUrl()
		{
			bool validUrl = new TfsUrlValidator(new WebCache()).IsValidTfsServerUrl("http://tfs03.codeplex.com");
			Assert.False(validUrl);
		}

		[Fact]
		public void CanAuthenticateServerUrl()
		{
			bool validUrl = new TfsUrlValidator(new WebCache()).IsValidTfsServerUrl(Settings.Default.ServerUrl);
			Assert.True(validUrl);
		}

		[Fact]
		public void WillCacheResults()
		{
			WebCache cache = new WebCache();
			new TfsUrlValidator(cache).IsValidTfsServerUrl(Settings.Default.ServerUrl);
			Assert.NotNull(cache.Get("IsValidTfsServerUrl_" + Settings.Default.ServerUrl));
		}

		[Fact]
		public void WillGetResultsFromCache()
		{
			WebCache cache = new WebCache();
			cache.Set("IsValidTfsServerUrl_blah", true);
			bool validUrl = new TfsUrlValidator(cache).IsValidTfsServerUrl("blah");
			Assert.True(validUrl);
		}
	}
}