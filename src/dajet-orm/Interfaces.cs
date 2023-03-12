using System;

namespace DaJet.Orm
{
    public interface IPersistent
    {
        PersistentState State { get; set; }
        event StateChangingEventHandler StateChanging;
        event StateChangedEventHandler StateChanged;
        void Save();
        void Kill();
        void Load();
        event SavingEventHandler Saving;
        event SavedEventHandler Saved;
        event KillingEventHandler Killing;
        event KilledEventHandler Killed;
        event LoadedEventHandler Loaded;
    }
    public interface IValueObject : IPersistent { }
    public interface IReferenceObject : IPersistent
    {
        Guid Identity { get; }
    }

    public interface IDataMapper
    {
        void Select(IPersistent entity);
        void Insert(IPersistent entity);
        void Update(IPersistent entity);
        void Delete(IPersistent entity);
    }

    public interface IIdentityMap
    {
        void Add(IReferenceObject item);
        bool Get(Guid identity, ref IReferenceObject item);
    }

    public interface IReferenceObjectFactory
    {
        /// <summary>
        /// Creates new instance of the given type
        /// </summary>
        IReferenceObject New(Type type); // New
        /// <summary>
        /// Creates new instance of the given type with specified identity value
        /// </summary>
        IReferenceObject New(Type type, Guid identity); // New
        /// <summary>
        /// Creates virtual instance having given type code and identity value
        /// </summary>
        IReferenceObject New(int typeCode, Guid identity); // Virtual
        /// <summary>
        /// Creates virtual instance having given type and identity value
        /// </summary>
        T New<T>(Guid identity) where T : IReferenceObject; // Virtual
    }

    public interface IPersistentContext
    {
        string Name { get; }
        string ConnectionString { get; }
        IDataMapper GetDataMapper(Type type);
        BiDictionary<int, Type> TypeCodes { get; }
        IReferenceObjectFactory Factory { get; }
    }
}