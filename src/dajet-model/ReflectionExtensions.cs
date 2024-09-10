using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DaJet.Model
{
    //NOTE: if (type.IsPublic && type.IsAbstract && type.IsSealed) /* that means static class */
    public static class ReflectionExtensions
    {
        public static bool IsStaticClass(this Type type)
        {
            return type.IsPublic && type.IsAbstract && type.IsSealed;
        }
        public static bool IsOption(this Type type)
        {
            return type.IsSubclassOf(typeof(OptionsBase));
        }
        public static bool IsOptionsFactory(this Type type, out Type options)
        {
            options = null;

            if (type.IsClass && !type.IsAbstract && !type.IsGenericType)
            {
                Type[] faces = type.GetInterfaces();

                for (int i = 0; i < faces.Length; i++)
                {
                    Type face = faces[i];

                    if (face.IsGenericType)
                    {
                        Type generic = face.GetGenericTypeDefinition();

                        if (generic == typeof(IOptionsFactory<>))
                        {
                            options = face.GetGenericArguments()[0];

                            return true;
                        }
                    }
                }
            }

            return false;
        }
        public static ConstructorInfo FindOptionsConstructor(this Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            ConstructorInfo[] constructors = type.GetConstructors();

            ConstructorInfo ctor = null;

            if (constructors.Length == 1)
            {
                ctor = constructors[0];
            }
            else
            {
                foreach (ConstructorInfo constructor in constructors)
                {
                    if (constructor.GetCustomAttribute<ActivatorUtilitiesConstructorAttribute>() is not null)
                    {
                        ctor = constructor; break;
                    }
                }
            }

            return ctor;
        }
        public static Type[] GetConstructorOptions(this Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            List<Type> options = new();

            ConstructorInfo ctor = type.FindOptionsConstructor();

            if (ctor is not null)
            {
                ParameterInfo[] parameters = ctor.GetParameters();

                int count = parameters.Length;

                for (int i = 0; i < count; i++)
                {
                    Type parameterType = parameters[i].ParameterType;

                    if (parameterType.IsSubclassOf(typeof(OptionsBase)))
                    {
                        options.Add(parameterType);
                    }
                }
            }

            return options.ToArray();
        }
        public static PropertyInfo[] GetWritableOptions(this Type type)
        {
            List<PropertyInfo> list = new();

            Type[] options = type.GetConstructorOptions();

            if (options is null || options.Length == 0)
            {
                return Array.Empty<PropertyInfo>();
            }

            for (int i = 0; i < options.Length; i++)
            {
                GetWritablePropertiesRecursively(options[i], list);
            }

            return list.ToArray();
        }
        private static void GetWritablePropertiesRecursively(Type type, List<PropertyInfo> list)
        {
            BindingFlags binding = BindingFlags.Instance | BindingFlags.Public;

            foreach (PropertyInfo property in type.GetProperties(binding))
            {
                if (!property.CanWrite)
                {
                    continue;
                }

                if (property.PropertyType.IsSimpleType())
                {
                    list.Add(property);
                }
                else if (property.PropertyType.IsClass)
                {
                    GetWritablePropertiesRecursively(property.PropertyType, list);
                }
            }
        }
        public static object CreateNewInstance(this Type type)
        {
            return Activator.CreateInstance(type);
        }
        public static bool IsSimpleType(this Type type)
        {
            return type.IsPrimitive
                || type.IsValueType
                || type == typeof(string)
                || type == typeof(Guid)
                || type == typeof(DateTime)
                || type == typeof(decimal);
        }
    }
}