using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Model
{
    public interface IReferenceObjectFactory
    {
        ///<summary>Creates new instance of the given type</summary>
        ReferenceObject New(Type type); // New
        ///<summary>Creates new instance of the given type with specified identity value</summary>
        ReferenceObject New(Type type, Guid identity); // New
        ///<summary>Creates virtual instance having given type code and identity value</summary>
        ReferenceObject New(int typeCode, Guid identity); // Virtual
        ///<summary>Creates virtual instance having given type and identity value</summary>
        T New<T>(Guid identity) where T : ReferenceObject; // Virtual
    }
    public sealed class PersistentObjectFactory : IReferenceObjectFactory
    {
        private delegate ReferenceObject ReferenceObjectConstructor(Guid identity, PersistentState state);

        private readonly IdentityMap _map;
        private readonly BiDictionary<int, Type> _codes;
        private readonly Dictionary<Type, ReferenceObjectConstructor> _constructors = new();

        public PersistentObjectFactory(BiDictionary<int, Type> typeCodes, bool useIdentityMap = true)
        {
            _codes = typeCodes ?? throw new ArgumentNullException(nameof(typeCodes));
            _map = (useIdentityMap) ? new IdentityMap() : null;
        }

        #region "CONSTRUCTOR IL GENERATION"
        private Type[] GetRefParameters()
        {
            return new Type[] { typeof(Guid), typeof(PersistentState) };
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
        private ReferenceObjectConstructor GenerateFactoryMethod(Type type)
        {
            Type[] parameters = GetRefParameters();

            DynamicMethod method = GenerateConstructorIL(type, parameters);

            ReferenceObjectConstructor ctor = (ReferenceObjectConstructor)method.CreateDelegate(typeof(ReferenceObjectConstructor));

            _constructors.Add(type, ctor);

            return ctor;
        }
        private ReferenceObjectConstructor GetConstructor(Type type)
        {
            if (!_constructors.TryGetValue(type, out ReferenceObjectConstructor ctor))
            {
                ctor = GenerateFactoryMethod(type);
            }
            return ctor;
        }
        #endregion

        #region "INTERFACE IMPLEMENTATION"
        public ReferenceObject New(Type type)
        {
            ReferenceObject item = GetConstructor(type)(Guid.NewGuid(), PersistentState.New);

            _map?.Add(item);

            return item;
        }
        public ReferenceObject New(Type type, Guid identity)
        {
            return CreateReferenceObject(GetConstructor(type), identity, PersistentState.New);
        }
        public ReferenceObject New(int typeCode, Guid identity)
        {
            return CreateReferenceObject(GetConstructor(_codes[typeCode]), identity, PersistentState.Virtual);
        }
        public T New<T>(Guid identity) where T : ReferenceObject
        {
            return (T)CreateReferenceObject(GetConstructor(typeof(T)), identity, PersistentState.Virtual);
        }
        private ReferenceObject CreateReferenceObject(ReferenceObjectConstructor ctor, Guid identity, PersistentState state)
        {
            if (_map is null)
            {
                return ctor(identity, state);
            }

            ReferenceObject item = null;

            if (_map.Get(identity, ref item)) { return item; }

            item = ctor(identity, state);

            _map.Add(item);

            return item;
        }
        #endregion
    }
}
// tip: if (type.IsPublic && type.IsAbstract && type.IsSealed) /* that means static class */