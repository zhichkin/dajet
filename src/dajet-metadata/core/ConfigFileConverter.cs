using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Core
{

    public sealed class ConfigFileConverter
    {
        public int Index { get; private set; }
        public ConfigFileConverter Parent { get; }
        public ConfigFileTokenHandler TokenHandler;
        private List<ConfigFileConverter> _list = new List<ConfigFileConverter>();
        public ConfigFileConverter() { }
        private ConfigFileConverter(ConfigFileConverter parent)
        {
            Parent = parent;
        }
        public int Count { get { return _list.Count; } }
        public ConfigFileConverter this[int i]
        {
            get
            {
                if (i > (_list.Count - 1))
                {
                    while (i > (_list.Count - 1))
                    {
                        _list.Add(new ConfigFileConverter(this) { Index = _list.Count });
                    }
                }
                return _list[i];
            }
            set
            {
                // setter is used to add or remove ConfigFileTokenHandler
                if (value != _list[i])
                {
                    throw new InvalidOperationException();
                }
            }
        }
        public ConfigFileConverter Root
        {
            get
            {
                if (Parent == null)
                {
                    return this;
                }

                ConfigFileConverter root = Parent;
                while (root.Parent != null)
                {
                    root = root.Parent;
                }

                return root;
            }
        }
        public ConfigFileConverter Path(int level, in int[] path)
        {
            ConfigFileConverter current = Root;

            for (int i = 0; i <= level; i++)
            {
                current = current[path[i]];
            }

            return current;
        }
        public ConfigFileTokenHandler GetTokenHandler(int level, in int[] path)
        {
            // This method is called by ConfigFileParser:
            // synchronizes converter with file reader and returns token handler if present
            // (after token handler has been invoked converter and file reader may get out of sync)

            ConfigFileConverter current = Root;

            for (int i = 0; i <= level; i++)
            {
                if (path[i] >= current.Count)
                {
                    return null;
                }
                current = current[path[i]];
            }

            return current.TokenHandler;
        }
        public static ConfigFileConverter operator +(ConfigFileConverter list, ConfigFileTokenHandler handler)
        {
            list.TokenHandler += handler;
            return list;
        }
        public static ConfigFileConverter operator -(ConfigFileConverter list, ConfigFileTokenHandler handler)
        {
            list.TokenHandler -= handler;
            return list;
        }
        public override string ToString()
        {
            string path = string.Empty;

            ConfigFileConverter list = this;

            while (list != null)
            {
                if (list.Parent == null)
                {
                    path = "root" + path;
                }
                else
                {
                    path = "." + list.Index.ToString() + path;
                }
                list = list.Parent;
            }
            return path;
        }
    }
}