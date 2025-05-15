using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    ///<summary>
    ///Идентификатор объекта СУБД:
    ///<br>Uuid - UUID объекта метаданных, в том числе реквизита или вспомогательной таблицы СУБД</br>
    ///<br>Code - Уникальный числовой код объекта СУБД (binary(4) - integer)</br>
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
        /// <summary>
        /// Дочерние идентификаторы основных объектов СУБД.<br/>
        /// Например, VT + LineNo или Reference + ReferenceChngR.
        /// </summary>
        public readonly List<DbName> Children { get; } = new();
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
        private readonly HashSet<string> _main = new()
        {
            MetadataTokens.VT,
            MetadataTokens.Fld,
            MetadataTokens.Acc,
            MetadataTokens.Enum,
            MetadataTokens.Chrc,
            MetadataTokens.Node,
            MetadataTokens.Task,
            MetadataTokens.Const,
            MetadataTokens.Document,
            MetadataTokens.Reference,
            MetadataTokens.AccRg,
            MetadataTokens.InfoRg,
            MetadataTokens.AccumRg
        };
        private readonly HashSet<string> _tref = new()
        {
            MetadataTokens.Acc,
            MetadataTokens.Enum,
            MetadataTokens.Chrc,
            MetadataTokens.Node,
            MetadataTokens.Task,
            MetadataTokens.Document,
            MetadataTokens.Reference
        };
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
            if (_tref.Contains(name))
            {
                _ = _codes.TryAdd(code, uuid); // Ссылочные типы данных
            }

            if (_cache.TryGetValue(uuid, out DbName entry))
            {
                if (_main.Contains(entry.Name)) // check if the root of DBNames is the main table
                {
                    entry.Children.Add(new DbName(uuid, code, name));
                }
                else
                {
                    DbName mainTable = new(uuid, code, name);
                    _cache[uuid] = mainTable; // the main table should be the root of DBNames
                    mainTable.Children.Add(entry);
                    mainTable.Children.AddRange(entry.Children);
                }
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