using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Model
{
    public interface IEntityFactory
    {
        void Register(int typeCode, Type entityType);
        T New<T>() where T : EntityObject; // New
        T New<T>(Guid identity) where T : EntityObject; // Virtual
    }
    public sealed class EntityFactory : IEntityFactory
    {
        private delegate EntityObject EntityObjectConstructor(IDataSource source, int typeCode, Guid identity);

        private readonly IDataSource _source;
        private readonly IdentityMap _map = new();
        private readonly BiDictionary<int, Type> _codes = new();
        private readonly Dictionary<Type, EntityObjectConstructor> _constructors = new();
        public EntityFactory(IDataSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }
        public void Register(int typeCode, Type entityType)
        {
            _codes.Add(typeCode, entityType);
        }

        #region "CONSTRUCTOR IL GENERATION"
        private Type[] GetConstructorParameters()
        {
            return new Type[] { typeof(IDataSource), typeof(int), typeof(Guid) };
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

        #region "INTERFACE IMPLEMENTATION"
        public T New<T>() where T : EntityObject
        {
            return New(typeof(T)) as T;
        }
        public T New<T>(Guid identity) where T : EntityObject
        {
            return New(typeof(T), identity) as T;
        }
        public EntityObject New(Type type)
        {
            EntityObjectConstructor ctor = GetConstructor(type);

            if (!_codes.TryGet(type, out int typeCode))
            {
                throw new InvalidOperationException();
            }

            EntityObject item = ctor(_source, typeCode, Guid.NewGuid());

            _map.Add(item);

            return item;
        }
        public EntityObject New(Type type, Guid identity)
        {
            EntityObjectConstructor ctor = GetConstructor(type);

            if (!_codes.TryGet(type, out int typeCode))
            {
                throw new InvalidOperationException();
            }

            return CreateReferenceObject(GetConstructor(type), typeCode, identity);
        }
        public EntityObject New(int typeCode, Guid identity)
        {
            return CreateReferenceObject(GetConstructor(_codes[typeCode]), typeCode, identity);
        }
        private EntityObject CreateReferenceObject(EntityObjectConstructor ctor, int typeCode, Guid identity)
        {
            if (_map is null)
            {
                return ctor(_source, typeCode, identity);
            }

            EntityObject item = null;

            if (_map.TryGet(identity, ref item))
            {
                return item;
            }

            item = ctor(_source, typeCode, identity);

            _map.Add(item);

            return item;
        }
        #endregion
    }
}
// tip: if (type.IsPublic && type.IsAbstract && type.IsSealed) /* that means static class */