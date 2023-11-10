using DaJet.Model;
using System.Reflection;

namespace DaJet.Flow
{
    //NOTE: if (type.IsPublic && type.IsAbstract && type.IsSealed) /* that means static class */
    public static class ReflectionExtensions
    {
        public static bool IsHandler(this Type type, out Type input, out Type output)
        {
            input = null;
            output = null;
            bool source = false;

            if (type.IsClass && !type.IsAbstract && !type.IsGenericType)
            {
                Type[] faces = type.GetInterfaces();

                for (int i = 0; i < faces.Length; i++)
                {
                    Type face = faces[i];

                    if (face.IsGenericType)
                    {
                        Type generic = face.GetGenericTypeDefinition();

                        if (generic == typeof(IInputBlock<>))
                        {
                            input = face.GetGenericArguments()[0];
                        }
                        else if (generic == typeof(IOutputBlock<>))
                        {
                            output = face.GetGenericArguments()[0];
                        }
                    }
                    else if (face == typeof(ISourceBlock))
                    {
                        source = true;
                    }
                }

                return source || input is not null || output is not null;
            }

            return false;
        }
        public static Type[] GetHandlerConstructorOptions(this Type type)
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

                    if (parameterType == typeof(IPipeline) ||
                        parameterType.IsSubclassOf(typeof(OptionsBase)))
                    {
                        options.Add(parameterType);
                    }
                }
            }

            return options.ToArray();
        }
    }
}