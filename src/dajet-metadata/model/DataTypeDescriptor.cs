using DaJet.Metadata.Core;
using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    ///<summary>Типы данных реквизитов объектов метаданных 1С</summary>
    [Flags] public enum DataTypeFlags : byte
    {
        None = 0x00,
        Binary = 0x01,
        String = 0x02,
        Numeric = 0x04,
        Boolean = 0x08,
        DateTime = 0x10,
        Reference = 0x20,
        ValueStorage = 0x40,
        UniqueIdentifier = 0x80
    }

    ///<summary>
    ///Описание типов: определяет допустимые типы данных для значений свойств прикладных объектов метаданных.
    ///<br>
    ///Составной тип данных может допускать использование нескольких типов данных для одного и того же свойства.
    ///</br>
    ///<br>
    ///Внимание! Следующие типы данных не допускают использование составного типа данных:
    ///</br>
    ///<br>
    ///"УникальныйИдентификатор", "ХранилищеЗначения", "ОпределяемыйТип", "Характеристика" и строка неограниченной длины.
    ///</br>
    ///</summary>
    public sealed class DataTypeDescriptor
    {
        private DataTypeFlags _flags = DataTypeFlags.None;
        public bool IsUndefined { get { return _flags == DataTypeFlags.None; } }

        #region "SIMPLE DATA TYPES"

        ///<summary>Тип значения свойства "УникальныйИдентификатор", binary(16). Не поддерживает составной тип данных.</summary>
        public bool IsUuid // SingleTypes.UniqueIdentifier
        {
            get { return (_flags & DataTypeFlags.UniqueIdentifier) == DataTypeFlags.UniqueIdentifier; }
            set
            {
                if (value)
                {
                    _flags = DataTypeFlags.UniqueIdentifier;
                    TypeCode = 0;
                    Reference = Guid.Empty;
                }
                else if (IsUuid)
                {
                    _flags = DataTypeFlags.None;
                }
            }
        }
        ///<summary>Типом значения свойства является byte[8] - версия данных, timestamp, rowversion. Не поддерживает составной тип данных.</summary>
        public bool IsBinary
        {
            get { return (_flags & DataTypeFlags.Binary) == DataTypeFlags.Binary; }
            set
            {
                if (value)
                {
                    _flags = DataTypeFlags.Binary;
                    TypeCode = 0;
                    Reference = Guid.Empty;
                }
                else if (IsBinary)
                {
                    _flags = DataTypeFlags.None;
                }
            }
        }
        ///<summary>Тип значения свойства "ХранилищеЗначения", varbinary(max). Не поддерживает составной тип данных.</summary>
        public bool IsValueStorage // SingleTypes.ValueStorage
        {
            get { return (_flags & DataTypeFlags.ValueStorage) == DataTypeFlags.ValueStorage; }
            set
            {
                if (value)
                {
                    _flags = DataTypeFlags.ValueStorage;
                    TypeCode = 0;
                    Reference = Guid.Empty;
                }
                else if (IsValueStorage)
                {
                    _flags = DataTypeFlags.None;
                }
            }
        }
        
        ///<summary>Типом значения свойства может быть "Булево" (поддерживает составной тип данных)</summary>
        public bool CanBeBoolean
        {
            get { return (_flags & DataTypeFlags.Boolean) == DataTypeFlags.Boolean; }
            set
            {
                if (IsUuid || IsValueStorage || IsBinary)
                {
                    if (value) { _flags = DataTypeFlags.Boolean; } // false is ignored
                }
                else if (value)
                {
                    _flags |= DataTypeFlags.Boolean;
                }
                else if (CanBeBoolean)
                {
                    _flags ^= DataTypeFlags.Boolean;
                }
            }
        }

        ///<summary>Типом значения свойства может быть "Строка" (поддерживает составной тип данных)</summary>
        public bool CanBeString
        {
            get { return (_flags & DataTypeFlags.String) == DataTypeFlags.String; }
            set
            {
                if (IsUuid || IsValueStorage || IsBinary)
                {
                    if (value) { _flags = DataTypeFlags.String; } // false is ignored
                }
                else if (value)
                {
                    _flags |= DataTypeFlags.String;
                }
                else if (CanBeString)
                {
                    _flags ^= DataTypeFlags.String;
                }
            }
        }
        ///<summary>Квалификатор: длина строки в символах. Неограниченная длина равна 0.</summary>
        public int StringLength { get; set; } = 10; // TODO: Строка неограниченной длины не поддерживает составной тип данных!
        ///<summary>
        ///Квалификатор: фиксированная (дополняется пробелами) или переменная длина строки.
        ///<br>
        ///Строка неограниченной длины (длина равна 0) всегда является переменной строкой.
        ///</br>
        ///</summary>
        public StringKind StringKind { get; set; } = StringKind.Variable;

        ///<summary>Типом значения свойства может быть "Число" (поддерживает составной тип данных)</summary>
        public bool CanBeNumeric
        {
            get { return (_flags & DataTypeFlags.Numeric) == DataTypeFlags.Numeric; }
            set
            {
                if (IsUuid || IsValueStorage || IsBinary)
                {
                    if (value) { _flags = DataTypeFlags.Numeric; } // false is ignored
                }
                else if (value)
                {
                    _flags |= DataTypeFlags.Numeric;
                }
                else if (CanBeNumeric)
                {
                    _flags ^= DataTypeFlags.Numeric;
                }
            }
        }
        ///<summary>Квалификатор: определяет допустимое количество знаков после запятой.</summary>
        public int NumericScale { get; set; } = 0;
        ///<summary>Квалификатор: определяет разрядность числа (сумма знаков до и после запятой).</summary>
        public int NumericPrecision { get; set; } = 10;
        ///<summary>Квалификатор: определяет возможность использования отрицательных значений.</summary>
        public NumericKind NumericKind { get; set; } = NumericKind.CanBeNegative;

        ///<summary>Типом значения свойства может быть "Дата" (поддерживает составной тип данных)</summary>
        public bool CanBeDateTime
        {
            get { return (_flags & DataTypeFlags.DateTime) == DataTypeFlags.DateTime; }
            set
            {
                if (IsUuid || IsValueStorage || IsBinary)
                {
                    if (value) { _flags = DataTypeFlags.DateTime; } // false is ignored
                }
                else if (value)
                {
                    _flags |= DataTypeFlags.DateTime;
                }
                else if (CanBeDateTime)
                {
                    _flags ^= DataTypeFlags.DateTime;
                }
            }
        }
        ///<summary>Квалификатор: определяет используемые части даты.</summary>
        public DateTimePart DateTimePart { get; set; } = DateTimePart.Date;

        #endregion

        #region "REFERENCE DATA TYPE"

        ///<summary>Типом значения свойства может быть "Ссылка" (поддерживает составной тип данных)</summary>
        public bool CanBeReference
        {
            get { return (_flags & DataTypeFlags.Reference) == DataTypeFlags.Reference; }
            set
            {
                if (IsUuid || IsValueStorage || IsBinary)
                {
                    if (value) { _flags = DataTypeFlags.Reference; } // false is ignored
                }
                else if (value)
                {
                    _flags |= DataTypeFlags.Reference;
                }
                else if (CanBeReference)
                {
                    _flags ^= DataTypeFlags.Reference;
                }
            }
        }
        ///<summary>
        ///Значение (по умолчанию) <see cref="Guid.Empty"/> допускает множественный ссылочный тип данных (TRef + RRef).
        ///<br>
        ///Конкретное значение <see cref="Guid"/> допускает использование единственного ссылочного типа данных (RRef).
        ///</br>
        ///<br>
        ///Выполняет роль квалификатора ссылочного типа данных.
        ///</br>
        ///</summary>
        public Guid Reference { get; set; } = Guid.Empty;
        ///<summary>
        ///Код типа объекта метаданных (дискриминатор)
        ///<br>Используется для формирования имени таблицы СУБД, а также как</br>
        ///<br>значение поля RTRef составного типа данных в записях таблиц СУБД.</br>
        ///<br><b>Значение по умолчанию: 0</b> (допускает множественный ссылочный тип данных)</br>
        ///<br>Выполняет роль квалификатора ссылочного типа данных.</br>
        ///</summary>
        public int TypeCode { get; set; } = 0;

        #endregion

        ///<summary>Проверяет является ли свойство составным типом данных</summary>
        public bool IsMultipleType
        {
            get
            {
                if (IsUuid || IsValueStorage || IsBinary) return false;

                int count = 0;
                if (CanBeString) count++;
                if (CanBeBoolean) count++;
                if (CanBeNumeric) count++;
                if (CanBeDateTime) count++;
                if (CanBeReference) count++;
                if (count > 1) return true;

                if (CanBeReference && Reference == Guid.Empty) return true;

                return false;
            }
        }
        public override string ToString()
        {
            if (IsMultipleType) return "Multiple";
            else if (IsUuid) return "Uuid";
            else if (IsBinary) return "Binary";
            else if (IsValueStorage) return "ValueStorage";
            else if (CanBeString) return "String";
            else if (CanBeBoolean) return "Boolean";
            else if (CanBeNumeric) return "Numeric";
            else if (CanBeDateTime) return "DateTime";
            else if (CanBeReference) return "Reference";
            else return "Undefined";
        }

        ///<summary>
        ///Применяет описание типов определяемого типа или характеристики к свойству объекта метаданных.
        ///<br>Используется методом <see cref="Configurator.ResolveAndCountReferenceTypes"/></br>
        ///</summary>
        ///<param name="source">Описание типов определяемого типа или характеристики.</param>
        internal void Apply(in DataTypeDescriptor source)
        {
            _flags = source._flags;

            StringKind = source.StringKind;
            StringLength = source.StringLength;

            NumericKind = source.NumericKind;
            NumericScale = source.NumericScale;
            NumericPrecision = source.NumericPrecision;

            DateTimePart = source.DateTimePart;
            
            TypeCode = source.TypeCode;
            Reference = source.Reference;
        }
        public DataTypeDescriptor Copy()
        {
            DataTypeDescriptor copy = new();

            copy.Apply(this);

            return copy;
        }
        
        public UnionType GetUnionType()
        {
            UnionType union = new();
            
            if (IsUuid) { union.IsUuid = true; }
            else if (IsBinary) { union.IsBinary = true; }
            else if (IsValueStorage) { union.IsBinary = true; }
            else
            {
                union.IsBoolean = CanBeBoolean;
                union.IsNumeric = CanBeNumeric;
                union.IsDateTime = CanBeDateTime;
                union.IsString = CanBeString;
                union.IsEntity = CanBeReference;
                union.TypeCode = TypeCode;
            }
            return union;
        }
        public bool IsUnionType(out bool canBeSimple, out bool canBeReference)
        {
            canBeSimple = false;
            canBeReference = false;

            if (IsUuid || IsValueStorage || IsBinary)
            {
                return false;
            }

            int count = 0;
            if (CanBeBoolean) { count++; }
            if (CanBeNumeric) { count++; }
            if (CanBeDateTime) { count++; }
            if (CanBeString) { count++; }
            if (count > 0) { canBeSimple = true; }

            if (CanBeReference)
            {
                count++; canBeReference = true;
            }

            if (count > 1) { return true; }

            if (canBeReference && TypeCode == 0) // !? Reference == Guid.Empty
            {
                return true;
            }

            return false;
        }
        public object GetDefaultValue()
        {
            if (IsUuid)
            {
                return Guid.Empty;
            }
            
            if (IsValueStorage || IsBinary)
            {
                return Array.Empty<byte>();
            }

            if (IsUnionType(out bool canBeSimple, out bool canBeReference))
            {
                if (canBeSimple)
                {
                    return null;
                }
                else // multiple reference type
                {
                    return Entity.Undefined;
                }
            }

            if (canBeReference)
            {
                return new Entity(TypeCode, Guid.Empty);
            }

            if (CanBeBoolean) { return false; }
            if (CanBeNumeric) { return 0.00M; }
            if (CanBeDateTime) { return new DateTime(1, 1, 1); }
            if (CanBeString) { return string.Empty; }

            return null;
        }
        public string GetDataTypeLiteral()
        {
            if (IsMultipleType) { return "union"; }
            else if (IsUuid) { return "uuid"; }
            else if (IsBinary) { return "binary"; }
            else if (IsValueStorage) { return "binary"; }
            else if (CanBeString) { return "string"; }
            else if (CanBeBoolean) { return "boolean"; }
            else if (CanBeNumeric) { return "number"; }
            else if (CanBeDateTime) { return "datetime"; }
            else if (CanBeReference) { return "entity"; }
            else { return "undefined"; }
        }
    }
    public static class DataTypeDescriptorExtensions
    {
        public static string GetDescriptionRu(this DataTypeDescriptor descriptor)
        {
            if (descriptor is null) { return "Неопределено"; }
            else if (descriptor.IsUuid) { return "УникальныйИдентификатор"; }
            else if (descriptor.IsValueStorage) { return "ХранилищеЗначения"; }
            else if (descriptor.IsBinary)
            {
                return "ДвоичныеДанные"; //TODO: binary(size) !?
            }
            else if (descriptor.IsUnionType(out bool canBeSimple, out bool canBeReference))
            {
                List<string> union = new();

                if (canBeSimple)
                {
                    if (descriptor.CanBeBoolean) { union.Add("Булево"); }

                    if (descriptor.CanBeNumeric)
                    {
                        union.Add($"Число({descriptor.NumericPrecision},{descriptor.NumericScale})");
                    }

                    if (descriptor.CanBeDateTime)
                    {
                        if (descriptor.DateTimePart == DateTimePart.Date) { union.Add("Дата"); }
                        else if (descriptor.DateTimePart == DateTimePart.Time) { union.Add("Время"); }
                        else { union.Add("ДатаВремя"); }
                    }

                    if (descriptor.CanBeString)
                    {
                        union.Add($"Строка({descriptor.StringLength},{descriptor.StringKind.GetNameRu()})");
                    }
                }

                if (canBeReference)
                {
                    union.Add($"Ссылка({descriptor.TypeCode},{descriptor.Reference})");
                }

                return string.Join(' ', union);
            }
            else if (descriptor.CanBeBoolean) { return "Булево"; }
            else if (descriptor.CanBeNumeric)
            {
                return $"Число({descriptor.NumericPrecision},{descriptor.NumericScale})";
            }
            else if (descriptor.CanBeDateTime)
            {
                if (descriptor.DateTimePart == DateTimePart.Date) { return "Дата"; }
                else if (descriptor.DateTimePart == DateTimePart.Time) { return "Время"; }
                else { return "ДатаВремя"; }
            }
            else if (descriptor.CanBeString)
            {
                return $"Строка({descriptor.StringLength},{descriptor.StringKind.GetNameRu()})";
            }
            else if (descriptor.CanBeReference)
            {
                return $"Ссылка({descriptor.TypeCode},{descriptor.Reference})";
            }

            return "Неопределено"; // Ошибка !!!
        }
    }
}