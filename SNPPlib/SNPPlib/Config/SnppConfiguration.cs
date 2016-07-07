using System;
using System.Configuration;

//TODO: Config transform to add section?
namespace SNPPlib.Config
{
    public class ServerCollection : ConfigurationElementCollection
    {
        internal const string _ElementName = "snpp";

        public ServerCollection()
        {
            ServerConfigElement details = (ServerConfigElement)CreateNewElement();
            if (!String.IsNullOrEmpty(details.Name))
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
                return _ElementName;
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
        private const string _Host = "host";
        private const string _LoginId = "loginId";
        private const string _Name = "name";
        private const string _Password = "password";
        private const string _Port = "port";

        [ConfigurationProperty(_Host, IsRequired = true)]
        public string Host
        {
            get
            {
                return (string)this[_Host];
            }
            set
            {
                this[_Host] = value;
            }
        }

        [ConfigurationProperty(_LoginId)]
        public string LoginId
        {
            get
            {
                return (string)this[_LoginId];
            }
            set
            {
                this[_LoginId] = value;
            }
        }

        [ConfigurationProperty(_Name, IsKey = true, DefaultValue = default(string))]
        public string Name
        {
            get
            {
                return (string)this[_Name];
            }
            set
            {
                this[_Name] = value;
            }
        }

        [ConfigurationProperty(_Password)]
        public string Password
        {
            get
            {
                return (string)this[_Password];
            }
            set
            {
                this[_Password] = value;
            }
        }

        [ConfigurationProperty(_Port, DefaultValue = (ushort)444)]
        public ushort Port
        {
            get
            {
                return (ushort)this[_Port];
            }
            set
            {
                this[_Port] = value;
            }
        }
    }

    public class SnppSettingsSection : ConfigurationSection
    {
        public const string SectionName = "snppSettings";

        [ConfigurationProperty("", IsDefaultCollection = true, IsRequired = true)]
        [ConfigurationCollection(typeof(ServerCollection), AddItemName = ServerCollection._ElementName)]
        public ServerCollection SnppServers
        {
            get
            {
                return (ServerCollection)base[String.Empty];
            }
        }
    }
}