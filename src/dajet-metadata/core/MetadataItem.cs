using System;

namespace DaJet.Metadata.Core
{
    public readonly struct MetadataItem
    {
        public static MetadataItem Empty { get; } = new();
        internal MetadataItem(Guid type, Guid uuid)
        {
            Type = type;
            Uuid = uuid;
        }
        internal MetadataItem(Guid type, Guid uuid, string name) : this(type, uuid)
        {
            Name = name;
        }
        public Guid Type { get; } = Guid.Empty;
        public Guid Uuid { get; } = Guid.Empty;
        public string Name { get; } = string.Empty;
        public override string ToString()
        {
            if (this == Empty)
            {
                return "Неопределено";
            }

            if (Type == SingleTypes.ValueStorage) { return "ХранилищеЗначения"; }
            if (Type == SingleTypes.UniqueIdentifier) { return "УникальныйИдентификатор"; }
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

            if (obj is not MetadataItem test)
            {
                return false;
            }

            return (this == test);
        }
        public static bool operator ==(MetadataItem left, MetadataItem right)
        {
            return left.Type == right.Type
                && left.Uuid == right.Uuid;
        }
        public static bool operator !=(MetadataItem left, MetadataItem right)
        {
            return !(left == right);
        }

        #endregion
    }
}