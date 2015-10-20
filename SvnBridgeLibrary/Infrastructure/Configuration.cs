using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Xml;

namespace SvnBridge.Infrastructure
{
    public static class Configuration
    {
        private enum ConfigSettings
        {
            CacheEnabled,
            CodePlexWorkItemUrl,
            DomainIncludesProjectName,
            DAVPropertiesIsAllowedRead,
            DAVPropertiesIsAllowedWrite,
            LogCancelErrors,
            LogPath,
            NetworkSvnUseInsecureNonLoopbackBind,
            PerfCountersAreMandatory,
            ProxyEncryptedPassword,
            ProxyUrl,
            ProxyPort,
            ProxyUseDefaultCredentials,
            ProxyUsername,
            // SvnPort should be _different_ from the TFS default port setting (8080),
            // to try to minimize port conflicts.
            // Side note: having chosen the wrong port setting here
            // may easily end up the fatally simple reason
            // for getting the client message
            // "could not connect to server".
            SvnPort,
            // Terrible misnomer: "TfsPort" does not end up as a "TFS port",
            // but the port that _we_ are offering for _our_ SvnBridge socket services
            // (which obviously are completely different from the services that TFS offers).
            // Thus I decided to semi-deprecate this completely misguided name,
            // with SvnPort being the new and strongly preferred name.
            TfsPort,
            TfsUrl,
            TfsTimeout,
            TfsProxyUrl,
            SCMWantCaseSensitiveItemMatch,
            TraceEnabled,
            UseCodePlexServers,
            UseProxy,
            ReadAllUserDomain,
            ReadAllUserName,
            ReadAllUserPassword,
            CodePlexAnonUserDomain,
            CodePlexAnonUserName,
            CodePlexAnonUserPassword,
        }
        private static readonly string userConfigFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\SvnBridge\3.0");

        private static Dictionary<string, string> userConfig = new Dictionary<string, string>();

        static Configuration()
        {
            ReadUserConfig();
        }

        public static void Save()
        {
            Directory.CreateDirectory(userConfigFolder);

            XmlDocument xml = new XmlDocument();
            xml.AppendChild(xml.CreateElement("configuration"));
            foreach (KeyValuePair<string, string> setting in userConfig)
            {
                if (setting.Value != null)
                {
                    XmlElement element = xml.CreateElement("setting");
                    element.Attributes.Append(xml.CreateAttribute("name"));
                    element.Attributes.Append(xml.CreateAttribute("value"));
                    element.Attributes["name"].Value = setting.Key;
                    element.Attributes["value"].Value = setting.Value.ToString();
                    xml["configuration"].AppendChild(element);
                }
            }

            string config = xml.InnerXml.Replace("><", ">\r\n<");
            File.WriteAllText(GetUserConfigFilePath(), config);
        }

        public static bool CacheEnabled
        {
            get { return ReadConfig<bool>(ConfigSettings.CacheEnabled, false); }
        }

        public static string CodePlexWorkItemUrl
        {
            get { return ReadConfig<string>(ConfigSettings.CodePlexWorkItemUrl, null); }
        }

        public static bool DAVPropertiesIsAllowedRead
        {
            get { return ReadConfig<bool>(ConfigSettings.DAVPropertiesIsAllowedRead, false); }
        }

        /// <remarks>
        /// DAV property storage handling currently still is problematic / incomplete.
        /// Since writing properties will be ensued by future read activity
        /// (which can end up problematic),
        /// we better don't enable writing by default,
        /// at least as long as we know that it's still problematic.
        /// </remarks>
        public static bool DAVPropertiesIsAllowedWrite
        {
            get { return ReadConfig<bool>(ConfigSettings.DAVPropertiesIsAllowedWrite, false); }
        }

        public static string LogPath
        {
            get { return ReadConfig<string>(ConfigSettings.LogPath, null); }
        }

        public static bool LogCancelErrors
        {
            get { return ReadConfig<bool>(ConfigSettings.LogCancelErrors, false); }
        }

        public static bool NetworkSvnUseInsecureNonLoopbackBind
        {
            get { return
            ReadConfig<bool>(ConfigSettings.NetworkSvnUseInsecureNonLoopbackBind, false); }
        }

        public static bool PerfCountersMandatory
        {
            get { return ReadConfig<bool>(ConfigSettings.PerfCountersAreMandatory, false); }
        }

        public static int SvnPort
        {
            get
            {
                // DON'T fall back to 8080 since that is TFS default port setting
                // (choose different setting - minimize conflicts).
                // Hmm... temporarily do change back to 8080...
                const int portDefault = 8080;
                int port = ReadConfig<int>(ConfigSettings.SvnPort, -1);
                // Maintain compatibility for users still using deprecated TfsPort name:
                if (-1 == port)
                {
                    port = ReadConfig<int>(ConfigSettings.TfsPort, -1);
                }
                if (-1 == port)
                {
                    port = portDefault;
                }
                return port;
            }
            set
            {
                string strValue = value.ToString();
                // Make sure that both SvnPort and deprecated TfsPort setting
                // are being maintained.
                // NOPE - since we now always prefer reading of SvnPort,
                // TfsPort does NOT need to be write-maintained
                // (which would potentially *newly* add it!) any more.
                userConfig[ConfigSettings.SvnPort.ToString()] = strValue;
                //userConfig[ConfigSettings.TfsPort.ToString()] = strValue;
            }
        }

        public static string TfsProxyUrl
        {
            get { return ReadConfig<string>(ConfigSettings.TfsProxyUrl, null); }
            set { userConfig[ConfigSettings.TfsProxyUrl.ToString()] = value; }
        }

        public static string TfsUrl
        {
            get { return ReadConfig<string>(ConfigSettings.TfsUrl, null); }
        }

        public static int TfsTimeout
        {
            get { return ReadConfig<int>(ConfigSettings.TfsTimeout, 900000); }
        }

        /// <summary>
        /// Central flag to indicate whether SvnBridge should try to do case-sensitive matching.
        /// This is very important in case of similar-name file renames (case-only changes) within a changeset,
        /// especially for users of case-sensitive filesystems (and perhaps also case-preserving ones).
        /// Having this setting activated is strongly recommended,
        /// however please note that that this activates some additional implementation checks
        /// and thus incurs some (potentially sizeable) processing overhead.
        /// For now this is configurable,
        /// to keep implementation of the previous behaviour available
        /// and to introduce this drastic (and thus potentially disrupting) change
        /// in a sufficiently benign way where needed.
        /// </summary>
        public static bool SCMWantCaseSensitiveItemMatch
        {
            get { return ReadConfig<bool>(ConfigSettings.SCMWantCaseSensitiveItemMatch, false); }
        }

        public static bool TraceEnabled
        {
            get { return ReadConfig<bool>(ConfigSettings.TraceEnabled, false); }
        }

        public static bool DomainIncludesProjectName
        {
            get { return ReadConfig<bool>(ConfigSettings.DomainIncludesProjectName, false); }
        }

        public static bool UseCodePlexServers
        {
            get { return ReadConfig<bool>(ConfigSettings.UseCodePlexServers, false); }
        }

        public static bool UseProxy
        {
            get { return ReadConfig<bool>(ConfigSettings.UseProxy, false); }
            set { userConfig[ConfigSettings.UseProxy.ToString()] = value.ToString(); }
        }

        public static string ProxyUrl
        {
            get { return ReadConfig<string>(ConfigSettings.ProxyUrl, ""); }
            set { userConfig[ConfigSettings.ProxyUrl.ToString()] = value.ToString(); }
        }

        public static int ProxyPort
        {
            get { return ReadConfig<int>(ConfigSettings.ProxyPort, 80); }
            set { userConfig[ConfigSettings.ProxyPort.ToString()] = value.ToString(); }
        }

        public static bool ProxyUseDefaultCredentials
        {
            get { return ReadConfig<bool>(ConfigSettings.ProxyUseDefaultCredentials, false); }
            set { userConfig[ConfigSettings.ProxyUseDefaultCredentials.ToString()] = value.ToString(); }
        }

        public static string ProxyUsername
        {
            get { return ReadConfig<string>(ConfigSettings.ProxyUsername, ""); }
            set { userConfig[ConfigSettings.ProxyUsername.ToString()] = value.ToString(); }
        }

        public static byte[] ProxyEncryptedPassword
        {
            get {
                string proxyEncryptedPassword = ReadConfig<string>(ConfigSettings.ProxyEncryptedPassword, null);
                if (proxyEncryptedPassword != null)
                    return Convert.FromBase64String(ReadConfig<string>(ConfigSettings.ProxyEncryptedPassword, null));
                else
                    return null;
            }
            set {
                if (value == null)
                    userConfig.Remove(ConfigSettings.ProxyEncryptedPassword.ToString());
                else
                    userConfig[ConfigSettings.ProxyEncryptedPassword.ToString()] = Convert.ToBase64String(value);
            }
        }

        public static string ReadAllUserDomain
        {
            get { return ReadConfig(ConfigSettings.ReadAllUserDomain, ""); }
            set { userConfig[ConfigSettings.ReadAllUserDomain.ToString()] = value; }
        }

        public static string ReadAllUserName
        {
            get { return ReadConfig(ConfigSettings.ReadAllUserName, ""); }
            set { userConfig[ConfigSettings.ReadAllUserName.ToString()] = value; }
        }

        public static string ReadAllUserPassword
        {
            get { return ReadConfig(ConfigSettings.ReadAllUserPassword, ""); }
            set { userConfig[ConfigSettings.ReadAllUserPassword.ToString()] = value; }
        }

        public static string CodePlexAnonUserDomain
        {
            get { return ReadConfig(ConfigSettings.CodePlexAnonUserDomain, ""); }
            set { userConfig[ConfigSettings.CodePlexAnonUserDomain.ToString()] = value; }
        }

        public static string CodePlexAnonUserName
        {
            get { return ReadConfig(ConfigSettings.CodePlexAnonUserName, ""); }
            set { userConfig[ConfigSettings.CodePlexAnonUserName.ToString()] = value; }
        }

        public static string CodePlexAnonUserPassword
        {
            get { return ReadConfig(ConfigSettings.CodePlexAnonUserPassword, ""); }
            set { userConfig[ConfigSettings.CodePlexAnonUserPassword.ToString()] = value; } 
        }

        public static object AppSettings(string name)
        {
            name = name.ToLower();
            if (name == ConfigSettings.CacheEnabled.ToString().ToLower()) return CacheEnabled;
            if (name == ConfigSettings.LogPath.ToString().ToLower()) return LogPath;
            if (name == ConfigSettings.PerfCountersAreMandatory.ToString().ToLower()) return PerfCountersMandatory;
            if (name == ConfigSettings.TfsUrl.ToString().ToLower()) return TfsUrl;
            if (name == ConfigSettings.DomainIncludesProjectName.ToString().ToLower()) return DomainIncludesProjectName;
            if (name == ConfigSettings.UseCodePlexServers.ToString().ToLower()) return UseCodePlexServers;
            if (name == ConfigSettings.TfsTimeout.ToString().ToLower()) return TfsTimeout;
            return null;
        }

        private static void ReadUserConfig()
        {
            string configFile = GetUserConfigFilePath();
            if (File.Exists(configFile))
            {
                XmlDocument xml = new XmlDocument();
                xml.InnerXml = File.ReadAllText(configFile);
                foreach (XmlElement node in xml.SelectNodes("//setting"))
                {
                    userConfig[node.Attributes["name"].Value] = node.Attributes["value"].Value;
                }
            }
        }

        private static string GetUserConfigFilePath()
        {
            return Path.Combine(userConfigFolder, "user.config");
        }

        private static T ReadConfig<T>(ConfigSettings setting, T defaultValue)
        {
            string name = setting.ToString();
            if (userConfig.ContainsKey(name))
                return (T)Convert.ChangeType(userConfig[name], typeof(T));

            if (ConfigurationManager.AppSettings[name] != null)
                return (T)Convert.ChangeType(ConfigurationManager.AppSettings[name], typeof(T));

            return defaultValue;
        }
    }
}
