using System;

namespace DaJet.Metadata.Core
{
    public static class SystemUuid
    {
        ///<summary>Идентификатор коллекции значений перечисления</summary>
        public static Guid Enumeration_Values = new Guid("bee0a08c-07eb-40c0-8544-5c364c171465");

        ///<summary>Идентификатор коллекции реквизитов табличной части</summary>
        public static Guid TablePart_Properties = new Guid("888744e1-b616-11d4-9436-004095e12fc7");

        ///<summary>Идентификатор коллекции реквизитов плана счетов</summary>
        public static Guid Account_Properties = new("6e65cbf5-daa8-4d8d-bef8-59723f4e5777");
        ///<summary>Идентификатор коллекции табличных частей плана счетов</summary>
        public static Guid Account_TableParts = new("4c7fec95-d1bd-4508-8a01-f1db090d9af8");
        ///<summary>Идентификатор коллекции признаков учёта плана счетов</summary>
        public static Guid Account_AccountingFlags = new("78bd1243-c4df-46c3-8138-e147465cb9a4");
        ///<summary>Идентификатор коллекции признаков учёта субконто плана счетов</summary>
        public static Guid Account_AccountingDimensionFlags = new("c70ca527-5042-4cad-a315-dcb4007e32a3");

        ///<summary>Идентификатор коллекции реквизитов справочника</summary>
        public static Guid Catalog_Properties = new Guid("cf4abea7-37b2-11d4-940f-008048da11f9");
        ///<summary>Идентификатор коллекции табличных частей справочника</summary>
        public static Guid Catalog_TableParts = new Guid("932159f9-95b2-4e76-a8dd-8849fe5c5ded");

        ///<summary>Идентификатор коллекции реквизитов документа</summary>
        public static Guid Document_Properties = new Guid("45e46cbc-3e24-4165-8b7b-cc98a6f80211");
        ///<summary>Идентификатор коллекции табличных частей документа</summary>
        public static Guid Document_TableParts = new Guid("21c53e09-8950-4b5e-a6a0-1054f1bbc274");

        ///<summary>Идентификатор коллекции реквизитов плана видов характеристик</summary>
        public static Guid Characteristic_Properties = new Guid("31182525-9346-4595-81f8-6f91a72ebe06");
        ///<summary>Идентификатор коллекции табличных частей плана видов характеристик</summary>
        public static Guid Characteristic_TableParts = new Guid("54e36536-7863-42fd-bea3-c5edd3122fdc");

        ///<summary>Идентификатор коллекции реквизитов плана обмена</summary>
        public static Guid Publication_Properties = new Guid("1a1b4fea-e093-470d-94ff-1d2f16cda2ab");
        ///<summary>Идентификатор коллекции табличных частей плана обмена</summary>
        public static Guid Publication_TableParts = new Guid("52293f4b-f98c-43ea-a80f-41047ae7ab58");

        ///<summary>Идентификатор коллекции ресурсов регистра сведений</summary>
        public static Guid InformationRegister_Measure = new Guid("13134202-f60b-11d5-a3c7-0050bae0a776");
        ///<summary>Идентификатор коллекции реквизитов регистра сведений</summary>
        public static Guid InformationRegister_Property = new Guid("a2207540-1400-11d6-a3c7-0050bae0a776");
        ///<summary>Идентификатор коллекции измерений регистра сведений</summary>
        public static Guid InformationRegister_Dimension = new Guid("13134203-f60b-11d5-a3c7-0050bae0a776");

        ///<summary>Идентификатор коллекции ресурсов регистра накопления</summary>
        public static Guid AccumulationRegister_Measure = new Guid("b64d9a41-1642-11d6-a3c7-0050bae0a776");
        ///<summary>Идентификатор коллекции реквизитов регистра накопления</summary>
        public static Guid AccumulationRegister_Property = new Guid("b64d9a42-1642-11d6-a3c7-0050bae0a776");
        ///<summary>Идентификатор коллекции измерений регистра накопления</summary>
        public static Guid AccumulationRegister_Dimension = new Guid("b64d9a43-1642-11d6-a3c7-0050bae0a776");

        ///<summary>Идентификатор коллекции макетов объекта метаданных</summary>
        public static Guid Template_Collection = new Guid("3daea016-69b7-4ed4-9453-127911372fe6");
    }
}