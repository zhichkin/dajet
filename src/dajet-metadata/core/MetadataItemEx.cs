using System;

namespace DaJet.Metadata.Core
{
    internal readonly struct MetadataItemEx
    {
        public static MetadataItemEx Empty { get; } = new();
        internal MetadataItemEx(Guid type, Guid uuid, string name, string file)
        {
            Type = type;
            Uuid = uuid;
            Name = name;
            File = file;
        }
        internal MetadataItemEx(Guid type, Guid uuid, string name, string file, Guid parent) : this(type, uuid, name, file)
        {
            Parent = parent;
        }
        public Guid Type { get; } = Guid.Empty;
        public Guid Uuid { get; } = Guid.Empty;
        public Guid Parent { get; } = Guid.Empty;
        public string Name { get; } = string.Empty;
        public string File { get; } = string.Empty;
        public MetadataItemEx Clone(Guid parent) { return new MetadataItemEx(Type, Uuid, Name, File, parent); }
        public override string ToString()
        {
            if (this == Empty)
            {
                return "Неопределено";
            }

            if (Type == SingleTypes.ValueStorage) { return "ХранилищеЗначения"; }
            if (Type == SingleTypes.Uniqueidentifier) { return "УникальныйИдентификатор"; }
            if (Type == ReferenceTypes.AnyReference) { return "ЛюбаяСсылка"; }
            if (Type == ReferenceTypes.Catalog) { return "СправочникСсылка"; }
            if (Type == ReferenceTypes.Document) { return "ДокументСсылка"; }
            if (Type == ReferenceTypes.Enumeration) { return "ПеречислениеСсылка"; }
            if (Type == ReferenceTypes.Publication) { return "ПланОбменаСсылка"; }
            if (Type == ReferenceTypes.Characteristic)
            {
                ///NOTE: Небольшой хак ¯\_(ツ)_/¯ <see cref="MetadataCache.ResolveReferenceType"/>
                if (Uuid == Guid.Empty)
                {
                    return "ПланВидовХарактеристикСсылка";
                }
                else
                {
                    return string.Format("Характеристика.{0}", Name);
                }
            }

            string typeName = MetadataTypes.ResolveNameRu(Type);

            if (string.IsNullOrEmpty(typeName))
            {
                return "???";
            }
            else
            {
                return string.Format("{0}.{1}", typeName, Name);
            }
        }

        #region " Переопределение методов сравнения "

        public override int GetHashCode()
        {
            return Uuid.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            if (obj == null) { return false; }

            if (obj is not MetadataItemEx test)
            {
                return false;
            }

            return (this == test);
        }
        public static bool operator ==(MetadataItemEx left, MetadataItemEx right)
        {
            return left.Type == right.Type && left.Uuid == right.Uuid;
        }
        public static bool operator !=(MetadataItemEx left, MetadataItemEx right)
        {
            return !(left == right);
        }

        #endregion
    }
}