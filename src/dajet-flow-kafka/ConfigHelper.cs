using DaJet.Model;
using System.Reflection;
using System.Text;

namespace DaJet.Flow.Kafka
{
    internal static class ConfigHelper
    {
        internal static string GetOptionKey(string propertyName)
        {
            StringBuilder key = new();

            for (int i = 0; i < propertyName.Length; i++)
            {
                char chr = propertyName[i];

                if (char.IsUpper(chr))
                {
                    if (i > 0) { key.Append('.'); }

                    key.Append(char.ToLowerInvariant(chr));
                }
                else
                {
                    key.Append(chr);
                }
            }

            return key.ToString();
        }
        internal static Dictionary<string, string> CreateConfigFromOptions(in OptionsBase options)
        {
            if (options is null) { throw new ArgumentNullException(nameof(options)); }

            Type type = options.GetType();

            string value;
            StringBuilder option = new();
            Dictionary<string, string> config = new();

            foreach (PropertyInfo property in type.GetProperties())
            {
                if (property.CanRead)
                {
                    value = property.GetValue(options)?.ToString();

                    if (string.IsNullOrWhiteSpace(value)) { continue; }

                    option.Clear();

                    for (int i = 0; i < property.Name.Length; i++)
                    {
                        char chr = property.Name[i];

                        if (char.IsUpper(chr))
                        {
                            if (i > 0) { option.Append('.'); }

                            option.Append(char.ToLowerInvariant(chr));
                        }
                        else
                        {
                            option.Append(chr);
                        }
                    }

                    config.Add(option.ToString(), value);
                }
            }

            return config;
        }
    }
}