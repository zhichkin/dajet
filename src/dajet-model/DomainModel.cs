using DaJet.Data;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Model
{
    public interface IDomainModel
    {
        IDomainModel ConfigureFromAssembly(in Assembly assembly);
        T New<T>() where T : EntityObject; // New
        T New<T>(Guid identity) where T : EntityObject; // Virtual
        EntityObject Create(int typeCode, Guid identity); // Virtual
    }
    public sealed class DomainModel : IDomainModel
    {
        private readonly IdentityMap _cache = new();
        private readonly IServiceProvider _services;
        private readonly BiDictionary<int, Type> _map = new();
        public DomainModel(IServiceProvider services)
        {
            _services = services;
        }
        public IDomainModel ConfigureFromAssembly(in Assembly assembly)
        {
            foreach (Type type in assembly.GetExportedTypes())
            {
                if (type.IsSubclassOf(typeof(EntityObject)))
                {
                    EntityAttribute attribute = type.GetCustomAttribute<EntityAttribute>();

                    if (attribute is not null)
                    {
                        _map.Add(attribute.TypeCode, type);
                    }
                }
            }

            return this;
        }
        public T New<T>() where T : EntityObject
        {
            T entity = ActivatorUtilities.CreateInstance<T>(_services);

            _cache.Add(entity);

            return entity;
        }
        public T New<T>(Guid identity) where T : EntityObject
        {
            EntityObject item = null;

            if (_cache.TryGet(identity, ref item))
            {
                return item as T;
            }

            if (!_map.TryGet(typeof(T), out int typeCode))
            {
                throw new UnknownTypeException(nameof(T));
            }

            T entity = ActivatorUtilities.CreateInstance<T>(_services, identity);

            _cache.Add(entity);

            return entity;
        }
        public EntityObject Create(int typeCode, Guid identity)
        {
            EntityObject entity = null;

            if (_cache.TryGet(identity, ref entity))
            {
                return entity;
            }

            if (!_map.TryGet(typeCode, out Type type))
            {
                throw new UnknownTypeException(typeCode.ToString());
            }

            entity = ActivatorUtilities.CreateInstance(_services, type, identity) as EntityObject;

            _cache.Add(entity);

            return entity;
        }

        #region "CONSTRUCTOR IL GENERATION"
        private readonly Dictionary<Type, EntityObjectConstructor> _constructors = new();
        private delegate EntityObject EntityObjectConstructor(int typeCode, Guid identity);
        private Type[] GetConstructorParameters()
        {
            return new Type[] { typeof(int), typeof(Guid) };
        }
        private EntityObjectConstructor GetConstructor(Type type)
        {
            if (!_constructors.TryGetValue(type, out EntityObjectConstructor ctor))
            {
                ctor = GenerateFactoryMethod(type);
            }
            return ctor;
        }
        private DynamicMethod GenerateConstructorIL(Type type, Type[] parameters)
        {
            ConstructorInfo info = type.GetConstructor(parameters);
            DynamicMethod method = new(string.Empty, type, parameters);

            ILGenerator il = method.GetILGenerator();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i == 0) il.Emit(OpCodes.Ldarg_0);
                else if (i == 1) il.Emit(OpCodes.Ldarg_1);
                else if (i == 2) il.Emit(OpCodes.Ldarg_2);
                else if (i == 3) il.Emit(OpCodes.Ldarg_3);
                else il.Emit(OpCodes.Ldarg_S, (byte)i);
            }
            il.Emit(OpCodes.Newobj, info);
            il.Emit(OpCodes.Ret);

            return method;
        }
        private EntityObjectConstructor GenerateFactoryMethod(Type type)
        {
            Type[] parameters = GetConstructorParameters();

            DynamicMethod method = GenerateConstructorIL(type, parameters);

            EntityObjectConstructor ctor = (EntityObjectConstructor)method.CreateDelegate(typeof(EntityObjectConstructor));

            _constructors.Add(type, ctor);

            return ctor;
        }
        #endregion
    }
}
// tip: if (type.IsPublic && type.IsAbstract && type.IsSealed) /* that means static class */