using System;

namespace DaJet.Metadata.Model
{
    public abstract class MetadataObject : IComparable
    {
        ///<summary>Идентификатор объекта метаданных</summary>
        public Guid Uuid { get; set; } = Guid.Empty;
        ///<summary>Имя объекта метаданных</summary>
        public string Name { get; set; } = string.Empty;
        ///<summary>Синоним объекта метаданных для представления в интерфейсе пользователя</summary>
        public string Alias { get; set; } = string.Empty;
        /// <summary>Пользовательский комментарий</summary>
        public string Comment { get; set; } = string.Empty;
        public int CompareTo(object other)
        {
            return CompareTo((MetadataObject)other);
        }
        public int CompareTo(MetadataObject other)
        {
            if (other == null) return 1;
            return Name.CompareTo(other.Name);
        }
        public override string ToString()
        {
            return string.Format("{0}.{1}", GetType().Name, Name);
        }
    }
}