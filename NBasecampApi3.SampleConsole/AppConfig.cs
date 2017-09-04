using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NBasecampApi3.SampleConsole
{
    public class AppConfig
    {
        public int AccountId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RedirectUrl { get; set; }
        public string RefreshToken { get; set; }

        public string AccessToken { get; set; }
        public string BasecampCookie { get; set; }
        public string Username { get; set; }
        public string VerificationCode { get; set; }

        public void Load()
        {
            Load(ConfigurationManager.AppSettings);
            LoadDebug();
        }

        public void Load(NameValueCollection appSettings)
        {
            if (appSettings == null) return;

            if (int.TryParse(appSettings["AccountId"], out int accountId))
            {
                AccountId = accountId;
            }
            ClientId = appSettings["ClientId"] ?? ClientId;
            ClientSecret = appSettings["ClientSecret"] ?? ClientSecret;
            RedirectUrl = appSettings["RedirectUrl"] ?? RedirectUrl;
            RefreshToken = appSettings["RefreshToken"] ?? RefreshToken;

            AccessToken = appSettings["AccessToken"] ?? AccessToken;
            BasecampCookie = appSettings["BasecampCookie"] ?? BasecampCookie;
            Username = appSettings["Username"] ?? Username;
            VerificationCode = appSettings["VerificationCode"] ?? VerificationCode;
        }

        [Conditional("DEBUG")]
        private void LoadDebug()
        {
            var debugFilePath = GetDebugConfigFilePathOrNull();
            if (debugFilePath == null)
            {
                return;
            }

            var appSettings = ParseAppSettings(debugFilePath);
            Load(appSettings);
        }

        private static string GetDebugConfigFilePathOrNull()
        {
            var directoryPath = Path.GetDirectoryName(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);

            // The `Debug.config` file could be in the same directory as 
            // `App.config`, or the App.config could be in `bin/Debug` when 
            // this is run from Visual Studio.  So we'll do a quick search up 
            // the path hierarchy to see if it exists somewhere.
            const int maxDepth = 3;
            int depth = 0;
            while (!string.IsNullOrWhiteSpace(directoryPath) && depth < maxDepth)
            {
                var configFilePath = Path.Combine(directoryPath, "Debug.config");
                if (File.Exists(configFilePath))
                {
                    return configFilePath;
                }
                directoryPath = Path.GetDirectoryName(directoryPath);
                depth++;
            }
            return null;
        }

        private static NameValueCollection ParseAppSettings(string configFilePath)
        {
            if (string.IsNullOrWhiteSpace(configFilePath))
            {
                return null;
            }
            var configDoc = XDocument.Load(configFilePath);
            var appSettingsElement = configDoc?.Root.Element("appSettings");
            var addElements = appSettingsElement?.Elements("add") 
                ?? Enumerable.Empty<XElement>();
            var appSettingKvPairs = addElements
                .Select(addElement => new KeyValuePair<string, string>(addElement.Attribute("key")?.Value, addElement.Attribute("value")?.Value))
                .Where(kvPair => !string.IsNullOrWhiteSpace(kvPair.Key));

            var appSettings = new NameValueCollection();
            foreach (var kv in appSettingKvPairs)
            {
                appSettings.Add(kv.Key, kv.Value);
            }
            return appSettings;
        }
    }
}
