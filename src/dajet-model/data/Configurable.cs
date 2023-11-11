using DaJet.Model;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DaJet.Flow
{
    public abstract class Configurable
    {
        public void Configure(in List<OptionRecord> options)
        {
            Type type = GetType();

            foreach (var option in options)
            {
                PropertyInfo property = type.GetProperty(option.Name);

                if (property is null) { continue; }

                object value = GetOptionValue(property.PropertyType, option.Value);

                if (value is not null) { property.SetValue(this, value); }
            }

            _Configure();
        }
        protected virtual void _Configure() { }
        private static object GetOptionValue(Type type, string value)
        {
            if (type == typeof(bool))
            {
                return (value.ToLower() == "true");
            }
            else if (type == typeof(int))
            {
                if (int.TryParse(value, out int number))
                {
                    return number;
                }
                else
                {
                    return null;
                }
            }
            else if (type == typeof(Guid))
            {
                if (Guid.TryParse(value, out Guid uuid))
                {
                    return uuid;
                }
                else
                {
                    return null;
                }
            }
            else if (type == typeof(DateTime))
            {
                if (DateTime.TryParse(value, out DateTime datetime))
                {
                    return datetime;
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