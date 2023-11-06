using System;
using System.Reflection;

namespace DaJet.Model
{
    public abstract class OptionsBase
    {
        public Entity Owner { get; set; } = Entity.Undefined;
        public bool Set(string name, string value)
        {
            Type type = GetType();

            BindingFlags binding = BindingFlags.Instance | BindingFlags.Public;

            PropertyInfo property = type.GetProperty(name, binding);

            if (property is null || !property.CanWrite)
            {
                return false;
            }

            Type valueType = property.PropertyType;

            object propertyValue;

            if (valueType == typeof(bool) && bool.TryParse(value, out bool boolean))
            {
                propertyValue = boolean;
            }
            else if (valueType == typeof(int) && int.TryParse(value, out int number))
            {
                propertyValue = number;
            }
            else if (valueType == typeof(DateTime) && DateTime.TryParse(value, out DateTime datetime))
            {
                propertyValue = datetime;
            }
            else if (valueType == typeof(string))
            {
                propertyValue = value;
            }
            else if (valueType.IsEnum && Enum.TryParse(valueType, value, true, out propertyValue))
            {
                // do nothing
            }
            else if (valueType == typeof(Guid) && Guid.TryParse(value, out Guid uuid))
            {
                propertyValue = uuid;
            }
            else
            {
                propertyValue = value;
            }

            property.SetValue(this, propertyValue);

            return true;
        }
    }
}