using System.Reflection;

namespace DaJet.Stream
{
    public static class ReflectionExtensions
    {
        public static bool IsProcessor(this Type type)
        {
            return type.IsSubclassOf(typeof(UserDefinedProcessor));
        }
        public static ConstructorInfo GetProcessorConstructor(this Type type)
        {
            ConstructorInfo[] constructors = type.GetConstructors();

            for (int i = 0; i < constructors.Length; i++)
            {
                ConstructorInfo constructor = constructors[i];

                if (constructor.IsPublic)
                {
                    ParameterInfo[] parameters = constructor.GetParameters();

                    if (parameters.Length == 1)
                    {
                        ParameterInfo parameter = parameters[0];

                        Type parameterType = parameter.ParameterType;

                        if (parameterType.IsByRef && !parameter.IsOut)
                        {
                            parameterType = parameterType.GetElementType();
                        }

                        if (parameterType == typeof(StreamScope))
                        {
                            return constructor;
                        }
                    }
                }
            }

            return null; // not found
        }
    }
}