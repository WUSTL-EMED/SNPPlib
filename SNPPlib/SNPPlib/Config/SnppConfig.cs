using System;
using System.Configuration;
using System.Globalization;

namespace SNPPlib.Config
{
    public static class SnppConfig
    {
        public static string GetHost(string name)
        {
            return GetConfig(name).Host;
        }

        public static string GetLoginId(string name)
        {
            return GetConfig(name).LoginId;
        }

        public static string GetPassword(string name)
        {
            return GetConfig(name).Password;
        }

        public static int GetPort(string name)
        {
            return GetConfig(name).Port;
        }

        private static ServerConfigElement GetConfig(string name)
        {
            var config = ConfigurationManager.GetSection(SnppSettingsSection.SectionName) as SnppSettingsSection;
            if (config == null)
                throw new ConfigurationErrorsException(Resource.ConfigNotFound);

            //This is messy, needs to be cleaned up.
            var empty = config.SnppServers.Count == 1 && String.IsNullOrEmpty(name) && String.IsNullOrEmpty(config.SnppServers[0].Name) ? config.SnppServers[0] : null;
            var server = name == null ? empty : config.SnppServers[name] ?? empty;
            if (server == null)
                throw new ConfigurationErrorsException(String.Format(CultureInfo.CurrentCulture, Resource.ConfigServerNotFound, name ?? String.Empty));

            return server;
        }
    }
}