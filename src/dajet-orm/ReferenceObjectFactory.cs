using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Orm
{
    public sealed class ReferenceObjectFactory : IReferenceObjectFactory
    {
        private delegate IReferenceObject ReferenceObjectConstructor(Guid identity, PersistentState state);

        private readonly IIdentityMap map;
        private readonly BiDictionary<int, Type> typeCodes;
        private readonly Dictionary<Type, ReferenceObjectConstructor> constructors = new Dictionary<Type, ReferenceObjectConstructor>();

        public ReferenceObjectFactory(BiDictionary<int, Type> typeCodes, bool useIdentityMap = true)
        {
            if (typeCodes == null) throw new ArgumentNullException("typeCodes");
            this.typeCodes = typeCodes;
            this.map = (useIdentityMap) ? new IdentityMap() : null;
        }

        # region " Constructor IL generation "

        private Type[] GetRefParameters()
        {
            return new Type[]
                {
                    typeof(Guid),
                    typeof(PersistentState)
                };
        }

        private DynamicMethod GenerateConstructorIL(Type type, Type[] parameters)
        {
            ConstructorInfo info = type.GetConstructor(parameters);
            DynamicMethod method = new DynamicMethod(string.Empty, type, parameters);

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

            constructors.Add(type, ctor);

            return ctor;
        }

        private ReferenceObjectConstructor GetConstructor(Type type)
        {
            ReferenceObjectConstructor ctor = null;
            if (!constructors.TryGetValue(type, out ctor))
            {
                ctor = GenerateFactoryMethod(type);
            }
            return ctor;
        }

        # endregion

        # region " Interface methods "

        public IReferenceObject New(Type type)
        {
            IReferenceObject item = GetConstructor(type)(Guid.NewGuid(), PersistentState.New);
            if (map != null)
            {
                map.Add(item);
            }
            return item;
        }

        public IReferenceObject New(Type type, Guid identity)
        {
            return CreateReferenceObject(GetConstructor(type), identity, PersistentState.New);
        }

        public IReferenceObject New(int typeCode, Guid identity)
        {
            return CreateReferenceObject(GetConstructor(typeCodes[typeCode]), identity, PersistentState.Virtual);
        }

        public T New<T>(Guid identity) where T : IReferenceObject
        {
            return (T)CreateReferenceObject(GetConstructor(typeof(T)), identity, PersistentState.Virtual);
        }

        private IReferenceObject CreateReferenceObject(ReferenceObjectConstructor ctor, Guid identity, PersistentState state)
        {
            if (map == null)
            {
                return ctor(identity, state);
            }

            IReferenceObject item = null;
            if (map.Get(identity, ref item))
            {
                return item;
            }

            item = ctor(identity, state);
            map.Add(item);
            return item;
        }

        # endregion
    }
}
// tip: if (type.IsPublic && type.IsAbstract && type.IsSealed) /* that means static class */