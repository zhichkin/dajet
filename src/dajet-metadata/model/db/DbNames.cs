using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    ///<summary>
    ///Идентификатор объекта СУБД:
    ///<br>Uuid - UUID объекта метаданных, в том числе реквизита или вспомогательной таблицы СУБД</br>
    ///<br>Code - Уникальный числовой код объекта СУБД</br>
    ///<br>Name - Буквенный идентификатор объекта СУБД, чаще всего это префикс его имени</br>
    ///<br>Children - идентификаторы <see cref="DbName"/> дочерних объектов СУБД, например, VT + LineNo или Reference + ReferenceChngR</br>
    ///</summary>
    public readonly struct DbName
    {
        internal DbName(Guid uuid, int code, string name)
        {
            Uuid = uuid;
            Code = code;
            Name = name;
        }
        public static DbName Empty
        {
            get { return new DbName(); }
        }
        public readonly int Code { get; } = 0;
        public readonly Guid Uuid { get; } = Guid.Empty;
        public readonly string Name { get; } = string.Empty;
        public readonly List<DbName> Children { get; } = new List<DbName>(); // VT + LineNo | Reference + ReferenceChngR
        public override string ToString()
        {
            return $"{Name} {{{Code}:{Uuid}}}";
        }
    }
    ///<summary>
    ///Коллекция идентификаторов СУБД <see cref="DbName"/> объектов метаданных конфигурации:
    ///<br>1. UUID объекта метаданных, в том числе реквизита или вспомогательной таблицы СУБД</br>
    ///<br>2. Уникальный числовой код объекта метаданных и СУБД</br>
    ///<br>3. Буквенный идентификатор объекта СУБД, например, Reference, Fld, VT, LineNo и т.п.</br>
    ///</summary>
    public sealed class DbNameCache
    {
        private readonly Dictionary<int, Guid> _codes = new();
        private readonly Dictionary<Guid, DbName> _cache = new();
        public IEnumerable<DbName> DbNames
        {
            get { return _cache.Values; }
        }
        public bool TryGet(Guid uuid, out DbName entry)
        {
            return _cache.TryGetValue(uuid, out entry);
        }
        public bool TryGet(int code, out Guid uuid)
        {
            return _codes.TryGetValue(code, out uuid);
        }
        public bool TryGet(int code, out DbName entry)
        {
            if (!_codes.TryGetValue(code, out Guid uuid))
            {
                entry = DbName.Empty;

                return false;
            }

            return _cache.TryGetValue(uuid, out entry);
        }
        public void Add(Guid uuid, int code, string name)
        {
            if (name == MetadataTokens.Chrc ||
                name == MetadataTokens.Enum ||
                name == MetadataTokens.Node ||
                name == MetadataTokens.Document ||
                name == MetadataTokens.Reference)
            {
                _ = _codes.TryAdd(code, uuid);
            }

            // NOTE: the case when child and parent items are in the wrong order is not assumed

            if (_cache.TryGetValue(uuid, out DbName entry))
            {
                entry.Children.Add(new DbName(uuid, code, name));
            }
            else
            {
                _cache.Add(uuid, new DbName(uuid, code, name));
            }
        }
        public void Clear()
        {
            _codes.Clear();
            _cache.Clear();
        }
    }
}