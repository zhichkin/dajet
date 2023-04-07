using DaJet.Flow.Model;
using System.Reflection;

namespace DaJet.Flow
{
    public abstract class Configurable
    {
        public virtual void Configure(in List<OptionItem> options)
        {
            Type type = GetType();

            foreach (var option in options)
            {
                PropertyInfo property = type.GetProperty(option.Name);

                if (property is null) { continue; }

                object value = GetOptionValue(property.PropertyType, option.Value);

                if (value is not null) { property.SetValue(this, value); }
            }
        }
        private static object GetOptionValue(Type type, string value)
        {
            if (type == typeof(bool))
            {
                return (value.ToLower() == "true");
            }
            else if (type == typeof(int))
            {
                if (int.TryParse(value, out int result))
                {
                    return result;
                }
                else
                {
                    return null;
                }
            }

            return value;
        }
    }
}