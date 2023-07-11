using DaJet.Flow;
using System.Reflection;
using System.Text;

namespace DaJet.Exchange.Kafka
{
    internal static class ConfigHelper
    {
        internal static Dictionary<string, string> CreateConfigFromOptions(in object block)
        {
            if (block is null) { throw new ArgumentNullException(nameof(block)); }

            Type type = block.GetType();

            string value;
            StringBuilder option = new();
            Dictionary<string, string> config = new();

            foreach (PropertyInfo property in type.GetProperties())
            {
                if (property.GetCustomAttribute<OptionAttribute>() is not null)
                {
                    value = property.GetValue(block)?.ToString();

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