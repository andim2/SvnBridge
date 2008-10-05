using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;

namespace SvnBridge.Infrastructure
{
    public class Configuration
    {
        private const string CACHE_ENABLED = "CacheEnabled";
        private const string LOG_PATH = "LogPath";
        private const string PERF_COUNTERS_MANDATORY = "PerfCountersAreMandatory";
        private const string TFS_URL = "TfsUrl";
        private const string DOMAIN_INCLUDES_PROJECT_NAME = "DomainIncludesProjectName";
        private const string USE_CODEPLEX_SERVERS = "UseCodePlexServers";

        public static bool CacheEnabled
        {
            get { return BoolConfig(CACHE_ENABLED, false); }
        }

        public static string LogPath
        {
            get { return ConfigurationManager.AppSettings[LOG_PATH]; }
        }

        public static bool PerfCountersMandatory
        {
            get { return BoolConfig(PERF_COUNTERS_MANDATORY, false); }
        }

        public static string TfsUrl
        {
            get { return ConfigurationManager.AppSettings[TFS_URL]; }
        }

        public static bool DomainIncludesProjectName
        {
            get { return BoolConfig(DOMAIN_INCLUDES_PROJECT_NAME, false); }
        }

        public static bool UseCodePlexServers
        {
            get { return BoolConfig(USE_CODEPLEX_SERVERS, false); }
        }

        private static bool BoolConfig(string name, bool defaultValue)
        {
            if (ConfigurationManager.AppSettings[name] != null)
                return bool.Parse(ConfigurationManager.AppSettings[name]);

            return defaultValue;
        }

        public static object AppSettings(string name)
        {
            name = name.ToLower();
            if (name == CACHE_ENABLED.ToLower()) return CacheEnabled;
            if (name == LOG_PATH.ToLower()) return LogPath;
            if (name == PERF_COUNTERS_MANDATORY.ToLower()) return PerfCountersMandatory;
            if (name == TFS_URL.ToLower()) return TfsUrl;
            if (name == DOMAIN_INCLUDES_PROJECT_NAME.ToLower()) return DomainIncludesProjectName;
            if (name == USE_CODEPLEX_SERVERS.ToLower()) return UseCodePlexServers;
            return null;
        }
    }
}
