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
        T New<T>() where T : EntityObject; // New
        T New<T>(Guid identity) where T : EntityObject; // Original
        EntityObject New(Type type); // New
        EntityObject New(Type type, Guid identity); // Original
        void Entity<T>(int typeCode);
        int GetTypeCode(Type entityType);
        Type GetEntityType(int typeCode);
        Entity GetEntity<T>(Guid identity);
    }
    public sealed class DomainModel : IDomainModel
    {
        private readonly Dictionary<int, Type> _types = new();
        private readonly Dictionary<Type, int> _codes = new();
        private readonly IServiceProvider _services;
        public DomainModel(IServiceProvider services)
        {
            _services = services;
            ConfigureDomainModel();
        }
        private void ConfigureDomainModel()
        {
            Entity<TreeNodeRecord>(10);
            Entity<PipelineRecord>(20);
            Entity<HandlerRecord>(30);
            Entity<OptionRecord>(40);
            Entity<InfoBaseRecord>(50);
            Entity<ScriptRecord>(60);
        }
        public void Entity<T>(int typeCode)
        {
            _ = _codes.TryAdd(typeof(T), typeCode);
            _ = _types.TryAdd(typeCode, typeof(T));
        }
        public int GetTypeCode(Type entityType)
        {
            if (_codes.TryGetValue(entityType, out int code))
            {
                return code;
            }
            return 0;
        }
        public Type GetEntityType(int typeCode)
        {
            if (_types.TryGetValue(typeCode, out Type type))
            {
                return type;
            }
            return null;
        }
        public Entity GetEntity<T>(Guid identity)
        {
            int typeCode = GetTypeCode(typeof(T));

            if (typeCode == 0)
            {
                throw new InvalidOperationException();
            }

            return new Entity(typeCode, identity);
        }
        public T New<T>() where T : EntityObject
        {
            if (!_codes.TryGetValue(typeof(T), out int typeCode))
            {
                throw new InvalidOperationException();
            }

            T entity = ActivatorUtilities.CreateInstance<T>(_services);

            typeof(T).GetProperty(nameof(EntityObject.TypeCode)).SetValue(entity, typeCode);
            typeof(T).GetProperty(nameof(EntityObject.Identity)).SetValue(entity, Guid.NewGuid());

            return entity;
        }
        public T New<T>(Guid identity) where T : EntityObject
        {
            if (!_codes.TryGetValue(typeof(T), out int typeCode))
            {
                throw new InvalidOperationException();
            }

            T entity = ActivatorUtilities.CreateInstance<T>(_services);

            typeof(T).GetProperty(nameof(EntityObject.TypeCode)).SetValue(entity, typeCode);
            typeof(T).GetProperty(nameof(EntityObject.Identity)).SetValue(entity, identity);

            entity.MarkAsOriginal();

            return entity;
        }
        public EntityObject New(Type type)
        {
            if (!type.IsSubclassOf(typeof(EntityObject)))
            {
                throw new InvalidOperationException();
            }

            if (!_codes.TryGetValue(type, out int typeCode))
            {
                throw new InvalidOperationException();
            }

            EntityObject entity = ActivatorUtilities.CreateInstance(_services, type) as EntityObject;

            type.GetProperty(nameof(EntityObject.TypeCode)).SetValue(entity, typeCode);
            type.GetProperty(nameof(EntityObject.Identity)).SetValue(entity, Guid.NewGuid());

            return entity;
        }
        public EntityObject New(Type type, Guid identity)
        {
            if (!type.IsSubclassOf(typeof(EntityObject)))
            {
                throw new InvalidOperationException();
            }

            if (!_codes.TryGetValue(type, out int typeCode))
            {
                throw new InvalidOperationException();
            }

            EntityObject entity = ActivatorUtilities.CreateInstance(_services, type) as EntityObject;

            type.GetProperty(nameof(EntityObject.TypeCode)).SetValue(entity, typeCode);
            type.GetProperty(nameof(EntityObject.Identity)).SetValue(entity, identity);

            entity.MarkAsOriginal();

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