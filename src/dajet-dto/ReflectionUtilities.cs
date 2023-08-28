using System.Reflection;
using System.Runtime.Loader;

namespace DaJet
{
    public static class ReflectionUtilities
    {
        public static Type ResolveTypeByName(string name)
        {
            Type? type = null;

            foreach (Assembly assembly in AssemblyLoadContext.Default.Assemblies)
            {
                if (Path.GetFileName(assembly.Location).StartsWith("DaJet"))
                {
                    type = assembly.GetType(name);

                    if (type != null)
                    {
                        return type;
                    }
                }
            }

            return type!;
        }
        public static Type GetTypeByNameOrFail(string name)
        {
            Type type = ResolveTypeByName(name);

            if (type == null)
            {
                throw new InvalidOperationException($"Failed to resolve type \"{name}\".");
            }

            return type;
        }
        public static Type ResolveServiceType(string serviceName)
        {
            return ResolveServiceType(serviceName, null!);
        }
        public static Type ResolveServiceType(string serviceName, string messageName)
        {
            Type serviceType = GetTypeByNameOrFail(serviceName);

            if (!serviceType.IsGenericType)
            {
                return serviceType;
            }

            if (string.IsNullOrWhiteSpace(messageName))
            {
                throw new InvalidOperationException($"Type parameter for generic service \"{serviceName}\" is not provided.");
            }

            Type messageType = GetTypeByNameOrFail(messageName);

            serviceType = serviceType.MakeGenericType(messageType);

            return serviceType;
        }

        //public static Type GetPipelineSourceBlockMessageType(Type sourceType)
        //{
        //    if (sourceType.IsGenericType)
        //    {
        //        return sourceType.GetGenericArguments()[0];
        //    }

        //    Type? baseType = sourceType.BaseType;

        //    if (baseType == null || baseType.GetGenericTypeDefinition() != typeof(Source<>))
        //    {
        //        throw new InvalidOperationException($"Pipeline source type does not inherit from DaJet.Flow.Source<T> abstract class.");
        //    }

        //    return baseType.GetGenericArguments()[0];
        //}
        //public static object CreateServiceInstance(IServiceProvider serviceProvider, Type serviceType, Dictionary<string, string> options)
        //{
        //    object? service = ActivatorUtilities.CreateInstance(serviceProvider, serviceType);

        //    if (service is IConfigurable configurable)
        //    {
        //        configurable.Configure(options);
        //    }

        //    if (service == null)
        //    {
        //        throw new InvalidOperationException($"Failed to create service of type {serviceType}.");
        //    }

        //    return service;
        //}
        //public static Type GetDataMapperFactory(Assembly assembly)
        //{
        //    foreach (Type type in assembly.GetTypes())
        //    {
        //        foreach (Type iface in type.GetInterfaces())
        //        {
        //            if (iface == typeof(IDataMapperFactory))
        //            {
        //                return type;
        //            }
        //        }
        //    }

        //    return null!;
        //}
    }
}