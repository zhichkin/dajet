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
        T New<T>() where T : EntityObject, new(); // New
        T New<T>(Guid identity) where T : EntityObject, new(); // Original
        EntityObject New(Type type); // New
        EntityObject New(Type type, Guid identity); // Original
        void Register(Type type, int code);
        void Entity<T>(int typeCode) where T : EntityObject;
        int GetTypeCode(Type entityType);
        Type GetEntityType(int typeCode);
        Entity GetEntity<T>(Guid identity) where T : EntityObject;
    }
    public sealed class DomainModel : IDomainModel
    {
        private readonly IServiceProvider _services;
        private readonly Dictionary<int, Type> _types;
        private readonly Dictionary<Type, int> _codes;
        public DomainModel(int capacity)
        {
            _types = new Dictionary<int, Type>(capacity);
            _codes = new Dictionary<Type, int>(capacity);
        }
        public DomainModel() : this(null) { }
        public DomainModel(IServiceProvider services)
        {
            _services = services;

            _types = new Dictionary<int, Type>(6);
            _codes = new Dictionary<Type, int>(6);
            
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
        public void Register(Type type, int code)
        {
            if (!type.IsSubclassOf(typeof(EntityObject)))
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            _ = _codes.TryAdd(type, code);
            _ = _types.TryAdd(code, type);
        }
        public void Entity<T>(int typeCode) where T : EntityObject
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
        public Entity GetEntity<T>(Guid identity) where T : EntityObject
        {
            int typeCode = GetTypeCode(typeof(T));

            if (typeCode == 0)
            {
                throw new InvalidOperationException();
            }

            return new Entity(typeCode, identity);
        }
        public T New<T>() where T : EntityObject, new()
        {
            if (!_codes.TryGetValue(typeof(T), out int typeCode))
            {
                throw new InvalidOperationException();
            }

            T entity = _services is null ? new T() : ActivatorUtilities.CreateInstance<T>(_services);

            typeof(T).GetProperty(nameof(EntityObject.TypeCode)).SetValue(entity, typeCode);
            typeof(T).GetProperty(nameof(EntityObject.Identity)).SetValue(entity, Guid.NewGuid());

            return entity;
        }
        public T New<T>(Guid identity) where T : EntityObject, new()
        {
            if (!_codes.TryGetValue(typeof(T), out int typeCode))
            {
                throw new InvalidOperationException();
            }

            T entity = _services is null ? new T() : ActivatorUtilities.CreateInstance<T>(_services);

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

            EntityObject entity = (_services is null
                ? Activator.CreateInstance(type)
                : ActivatorUtilities.CreateInstance(_services, type))
                as EntityObject;

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

            EntityObject entity = (_services is null
                ? Activator.CreateInstance(type)
                : ActivatorUtilities.CreateInstance(_services, type))
                as EntityObject;

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