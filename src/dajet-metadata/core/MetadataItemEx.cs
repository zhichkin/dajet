using System;

namespace DaJet.Metadata.Core
{
    public readonly struct MetadataItemEx
    {
        internal static MetadataItemEx Empty { get; } = new();
        internal MetadataItemEx(Guid extension, Guid type, Guid uuid, string name, string file, ExtensionType extensionType)
        {
            Extension = extension;
            Type = type;
            Uuid = uuid;
            Name = name;
            File = file;
            ExtensionType = extensionType;
        }
        internal MetadataItemEx(Guid extension, Guid type, Guid uuid, string name, string file, Guid parent, ExtensionType extensionType)
            : this(extension, type, uuid, name, file, extensionType)
        {
            Parent = parent;
        }
        public Guid Extension { get; } = Guid.Empty;
        public Guid Type { get; } = Guid.Empty;
        public Guid Uuid { get; } = Guid.Empty;
        public Guid Parent { get; } = Guid.Empty;
        public string Name { get; } = string.Empty;
        public string File { get; } = string.Empty;
        public ExtensionType ExtensionType { get; }
        /// <summary>
        /// Cобственный объект расширения (не заимствованный из основной конфигурации)
        /// </summary>
        internal bool IsExtensionOwnObject { get { return Uuid == Parent; } }
        internal MetadataItemEx SetParent(Guid parent)
        {
            return new MetadataItemEx(Extension, Type, Uuid, Name, File, parent, ExtensionType);
        }
        public override string ToString()
        {
            if (this == Empty) { return "Неопределено"; }

            if (Type == SingleTypes.ValueStorage) { return "ХранилищеЗначения"; }
            if (Type == SingleTypes.UniqueIdentifier) { return "УникальныйИдентификатор"; }
            if (Type == ReferenceTypes.AnyReference) { return "ЛюбаяСсылка"; }
            if (Type == ReferenceTypes.Catalog) { return "СправочникСсылка"; }
            if (Type == ReferenceTypes.Document) { return "ДокументСсылка"; }
            if (Type == ReferenceTypes.Enumeration) { return "ПеречислениеСсылка"; }
            if (Type == ReferenceTypes.Publication) { return "ПланОбменаСсылка"; }
            if (Type == ReferenceTypes.Characteristic)
            {
                ///NOTE: Небольшой хак ¯\_(ツ)_/¯ <see cref="OneDbMetadataProvider.ResolveReferenceType"/>
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
            return left.Extension == right.Extension && left.Type == right.Type && left.Uuid == right.Uuid;
        }
        public static bool operator !=(MetadataItemEx left, MetadataItemEx right)
        {
            return !(left == right);
        }

        #endregion
    }
}