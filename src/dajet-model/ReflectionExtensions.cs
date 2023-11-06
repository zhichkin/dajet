using System;

namespace DaJet.Model
{
    //NOTE: if (type.IsPublic && type.IsAbstract && type.IsSealed) /* that means static class */
    public static class ReflectionExtensions
    {
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
        public static object CreateNewInstance(this Type type)
        {
            return Activator.CreateInstance(type);
        }
    }
}