using DaJet.Data;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public sealed class DbSchema
    {
        public Dictionary<int, DbSchemaTable> Tables { get; } = new Dictionary<int, DbSchemaTable>();
    }
    public sealed class DbSchemaTable
    {
        public int Code { get; set; } // Unique code
        public string Name { get; set; } // Database object name (metadata object token + Code)
        public string Parent { get; set; } // Parent database object name - used by TablePart table
    }
    public sealed class DbSchemaColumn
    {
        public string Name { get; set; } // Database object name
        public List<DbSchemaField> Fields { get; } = new List<DbSchemaField>();
    }
    public sealed class DbSchemaField
    {
        public string Name { get; set; } // Database object name
        public ColumnPurpose Purpose { get; set; }
    }
}

// 1 - List<DbSchemaTable>
// 1.0 - количество таблиц СУБД
// 1.N - корень описания таблицы СУБД - DbSchemaTable
// 1.N.0 - имя таблицы СУБД
// 1.N.1 - "N"
// 1.N.2 - код таблицы СУБД (см. файл DBNames таблицы Params)
// 1.N.3 - ""
// 1.N.4 - List<DbSchemaColumn>
// 1.N.4.0 - количество колонок таблицы СУБД
// 1.N.4.C - корень описания колонки СУБД - DbSchemaColumn
// 1.N.4.C.0 - имя колонки
// 1.N.4.C.1 - "0"
// 1.N.4.C.2 - List<DbSchemaField>
// 1.N.4.C.2.0 - количество полей колонки таблицы СУБД
// 1.N.4.C.2.F - корень описания поля колонки таблицы СУБД - DbSchemaField
// 1.N.4.C.2.F.0 - тип поля {E,V,B,L,T,N,S,R}
// 1.N.4.C.2.F.1 - квалификатор типа 1 (размер) некорректно отображается для строк - всегда int.MaxValue
// 1.N.4.C.2.F.2 - квалификатор типа 2
// 1.N.4.C.2.F.3 - имя ссылочного типа, если single type, иначе пустая строка (RRef) или (RRef + TRef)
// 1.N.4.C.2.F.4 - какое-то перечисление, вероятно тип ссылки: 2 - реквизит "Ссылка", 3 - single ref, 4 - multiple ref

// 1.N.5 - List<DbSchemaTable> коллекция табличных частей = [1]
// 1.N.5.0 - количество табличных частей
// 1.N.5.T - корень описания табличной части
// 1.N.5.T.0 - имя таблицы СУБД (содержит числовой код таблицы - см. файл DBNames таблицы Params)
// 1.N.5.T.1 - "I"
// 1.N.5.T.2 - "0"
// 1.N.5.T.3 - имя ссылочного типа, владельца табличной части = 1.N.0
// 1.N.5.T.4 - List<DbSchemaColumn> = 1.N.4

// 1.N.6 - коллекция индексов таблицы СУБД

// List<DbSchemaColumn> включает в себя в том числе общие реквизиты !!!