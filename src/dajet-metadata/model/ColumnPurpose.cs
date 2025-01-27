using DaJet.Data;
using System;

namespace DaJet.Metadata.Model
{
    public enum ColumnPurpose
    {
        ///<summary>Single type value of the property. _Fld
        ///<br>Default purpose - single value storage.</br>
        ///</summary>
        Default,
        ///<summary><b>Discriminated (tagged) union</b>
        ///<br>Составной тип данных _Fld + _TYPE binary(1)</br>
        ///<br><b>0x01</b> - Неопределено = null</br>
        ///<br><b>0x02</b> - Булево = boolean</br>
        ///<br><b>0x03</b> - Число = decimal</br>
        ///<br><b>0x04</b> - Дата = DateTime</br>
        ///<br><b>0x05</b> - Строка = string</br>
        ///<br><b>0x06</b> - byte[] = binary</br>
        ///<br><b>0x07</b> - ?</br>
        ///<br><b>0x08</b> - Ссылка = Entity</br>
        ///</summary>
        Tag,
        ///<summary>0x02 Boolean value (bool) _Fld + _L binary(1)</summary>
        Boolean,
        ///<summary>0x03 Numeric value (decimal | int) _Fld + _N numeric(p,s)</summary>
        Numeric,
        ///<summary>0x04 Date and time value (DateTime) _Fld + _T datetime2</summary>
        DateTime,
        ///<summary>0x05 String value (string) _Fld + _S nvarchar | nchar</summary>
        String,
        ///<summary>0x06 Binary value (byte[]) _Fld + _B binary(max)</summary>
        Binary,
        ///<summary>Type code of the reference type (int) _Fld + _RTRef binary(4)</summary>
        TypeCode,
        ///<summary>0x08 Reference type primary key value (Guid) _Fld + _RRRef binary(16)</summary>
        Identity
    }
    public static class ColumnPurposeExtensions
    {
        public static string GetNameRu(this ColumnPurpose purpose)
        {
            if (purpose == ColumnPurpose.Default) { return "Значение"; }
            else if (purpose == ColumnPurpose.Tag) { return "Дискриминатор"; }
            else if (purpose == ColumnPurpose.Boolean) { return "Булево"; }
            else if (purpose == ColumnPurpose.Numeric) { return "Число"; }
            else if (purpose == ColumnPurpose.DateTime) { return "ДатаВремя"; }
            else if (purpose == ColumnPurpose.String) { return "Строка"; }
            else if (purpose == ColumnPurpose.Binary) { return "ДвоичныеДанные"; }
            else if (purpose == ColumnPurpose.TypeCode) { return "КодСсылки"; }
            else if (purpose == ColumnPurpose.Identity) { return "Ссылка"; }
            else
            {
                return "Значение";
            }
        }
        public static ColumnPurpose ParseColumnPurpose(string fieldName)
        {
            char L = char.Parse("L");
            char N = char.Parse("N");
            char T = char.Parse("T");
            char S = char.Parse("S");
            char B = char.Parse("B");

            char test = fieldName[fieldName.Length - 1];

            if (char.IsDigit(test)) return ColumnPurpose.Default;

            if (test == L)
            {
                return ColumnPurpose.Boolean;
            }
            else if (test == N)
            {
                return ColumnPurpose.Numeric;
            }
            else if (test == T)
            {
                return ColumnPurpose.DateTime;
            }
            else if (test == S)
            {
                return ColumnPurpose.String;
            }
            else if (test == B)
            {
                return ColumnPurpose.Binary;
            }

            string TYPE = "TYPE";
            string TRef = "TRef";
            string RRef = "RRef";

            string postfix = fieldName.Substring(fieldName.Length - 4);

            if (postfix == TYPE)
            {
                return ColumnPurpose.Tag;
            }
            else if (postfix == TRef)
            {
                return ColumnPurpose.TypeCode;
            }
            else if (postfix == RRef)
            {
                return ColumnPurpose.Identity;
            }

            return ColumnPurpose.Default;
        }
        public static string GetLiteral(this ColumnPurpose purpose)
        {
            if (purpose == ColumnPurpose.Tag)
            {
                return "TYPE";
            }
            else if (purpose == ColumnPurpose.Boolean)
            {
                return "L";
            }
            else if (purpose == ColumnPurpose.Numeric)
            {
                return "N";
            }
            else if (purpose == ColumnPurpose.DateTime)
            {
                return "T";
            }
            else if (purpose == ColumnPurpose.String)
            {
                return "S";
            }
            else if (purpose == ColumnPurpose.Binary)
            {
                return "B";
            }
            else if (purpose == ColumnPurpose.TypeCode)
            {
                return "TRef";
            }
            else if (purpose == ColumnPurpose.Identity)
            {
                return "RRef";
            }

            return string.Empty; // ColumnPurpose.Default
        }
        public static string GetBinaryLiteral(this ColumnPurpose purpose, int value)
        {
            byte[] bytes = BitConverter.GetBytes(value); // discriminator or type code

            if (purpose == ColumnPurpose.Tag) // size is 1 byte
            {
                if (BitConverter.IsLittleEndian)
                {
                    return "0x" + DbUtilities.ByteArrayToString(new byte[] { bytes[0] });
                }
                else
                {
                    return "0x" + DbUtilities.ByteArrayToString(new byte[] { bytes[3] });
                }
            }
            else // size is 4 bytes
            {
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }
                return "0x" + DbUtilities.ByteArrayToString(bytes); // '\\x00000024'
            }
        }
        public static UnionTag GetUnionTag(this ColumnPurpose purpose)
        {
            if (purpose == ColumnPurpose.Tag)
            {
                return UnionTag.Tag;
            }
            else if (purpose == ColumnPurpose.Boolean)
            {
                return UnionTag.Boolean;
            }
            else if (purpose == ColumnPurpose.Numeric)
            {
                return UnionTag.Numeric;
            }
            else if (purpose == ColumnPurpose.DateTime)
            {
                return UnionTag.DateTime;
            }
            else if (purpose == ColumnPurpose.String)
            {
                return UnionTag.String;
            }
            else if (purpose == ColumnPurpose.Binary)
            {
                return UnionTag.Binary;
            }
            else if (purpose == ColumnPurpose.TypeCode)
            {
                return UnionTag.TypeCode;
            }
            else if (purpose == ColumnPurpose.Identity)
            {
                return UnionTag.Entity;
            }

            return UnionTag.Undefined; // ColumnPurpose.Default
        }
        public static string GetColumnPostfix(this ColumnPurpose purpose)
        {
            if (purpose == ColumnPurpose.Default) { return string.Empty; }
            else if (purpose == ColumnPurpose.Tag) { return "_TYPE"; }
            else if (purpose == ColumnPurpose.Boolean) { return "_L"; }
            else if (purpose == ColumnPurpose.Numeric) { return "_N"; }
            else if (purpose == ColumnPurpose.DateTime) { return "_T"; }
            else if (purpose == ColumnPurpose.String) { return "_S"; }
            else if (purpose == ColumnPurpose.Binary) { return "_B"; }
            else if (purpose == ColumnPurpose.TypeCode) { return "_TRef"; }
            else if (purpose == ColumnPurpose.Identity) { return "_RRef"; }

            return string.Empty;
        }
    }
}