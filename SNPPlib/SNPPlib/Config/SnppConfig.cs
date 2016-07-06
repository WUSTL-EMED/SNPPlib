using System;
using System.Configuration;

//TODO: Config for server?
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

        public static ushort GetPort(string name)
        {
            return GetConfig(name).Port;
        }

        private static ServerConfigElement GetConfig(string name)
        {
            var config = ConfigurationManager.GetSection("snppSettings") as SnppSettingsSection;
            if (config == null)
                throw new ConfigurationErrorsException(Resource.ConfigNotFound);

            //This is messy, needs to be cleaned up.
            var empty = config.SnppServers.Count == 1 && String.IsNullOrEmpty(name) && String.IsNullOrEmpty(config.SnppServers[0].Name) ? config.SnppServers[0] : null;
            var server = name == null ? empty : config.SnppServers[name] ?? empty;
            if (server == null)
                throw new ConfigurationErrorsException(String.Format(Resource.ConfigServerNotFound, name ?? String.Empty));

            return server;
        }
    }

    public class ServerCollection : ConfigurationElementCollection
    {
        public ServerCollection()
        {
            ServerConfigElement details = (ServerConfigElement)CreateNewElement();
            if (details.Name != "")
                Add(details);
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }

        protected override string ElementName
        {
            get
            {
                return "snpp";
            }
        }

        public ServerConfigElement this[int index]
        {
            get
            {
                return (ServerConfigElement)BaseGet(index);
            }
            set
            {
                if (BaseGet(index) != null)
                    BaseRemoveAt(index);
                BaseAdd(index, value);
            }
        }

        new public ServerConfigElement this[string name]
        {
            get
            {
                return (ServerConfigElement)BaseGet(name);
            }
        }

        public void Add(ServerConfigElement details)
        {
            BaseAdd(details);
        }

        public void Clear()
        {
            BaseClear();
        }

        public int IndexOf(ServerConfigElement details)
        {
            return BaseIndexOf(details);
        }

        public void Remove(ServerConfigElement details)
        {
            if (BaseIndexOf(details) >= 0)
                BaseRemove(details.Name);
        }

        public void Remove(string name)
        {
            BaseRemove(name);
        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }

        protected override void BaseAdd(ConfigurationElement element)
        {
            BaseAdd(element, false);
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new ServerConfigElement();
        }

        protected override Object GetElementKey(ConfigurationElement element)
        {
            return ((ServerConfigElement)element).Name;
        }
    }

    public class ServerConfigElement : ConfigurationElement
    {
        [ConfigurationProperty("host", IsRequired = true)]
        public string Host
        {
            get
            {
                return (string)this["host"];
            }
            set
            {
                this["host"] = value;
            }
        }

        [ConfigurationProperty("loginId")]
        public string LoginId
        {
            get
            {
                return (string)this["loginId"];
            }
            set
            {
                this["loginId"] = value;
            }
        }

        [ConfigurationProperty("name", IsKey = true, DefaultValue = default(string))]
        public string Name
        {
            get
            {
                return (string)this["name"];
            }
            set
            {
                this["name"] = value;
            }
        }

        [ConfigurationProperty("password")]
        public string Password
        {
            get
            {
                return (string)this["password"];
            }
            set
            {
                this["password"] = value;
            }
        }

        [ConfigurationProperty("port", DefaultValue = (ushort)444)]
        public ushort Port
        {
            get
            {
                return (ushort)this["port"];
            }
            set
            {
                this["port"] = value;
            }
        }
    }

    public class SnppSettingsSection : ConfigurationSection
    {
        [ConfigurationProperty("", IsDefaultCollection = true, IsRequired = true)]
        [ConfigurationCollection(typeof(ServerCollection), AddItemName = "snpp")]
        public ServerCollection SnppServers
        {
            get
            {
                return (ServerCollection)base[""];
            }
        }
    }
}