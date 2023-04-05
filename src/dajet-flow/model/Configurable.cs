using System.Reflection;

namespace DaJet.Flow
{
    public abstract class Configurable
    {
        public void Configure(in Dictionary<string, string> options)
        {
            Type type = GetType();

            foreach (var option in options)
            {
                PropertyInfo property = type.GetProperty(option.Key);

                if (property is null) { continue; }

                object value = GetOptionValue(property.PropertyType, option.Value);

                if (value is not null) { property.SetValue(this, value); }
            }
        }
        private static object GetOptionValue(Type type, string value)
        {
            if (type == typeof(int))
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