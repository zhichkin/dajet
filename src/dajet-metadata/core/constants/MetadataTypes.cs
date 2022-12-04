using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Core
{
    public static class MetadataTypes
    {
        public static Guid InfoBase = Guid.Empty; // Корень конфигурации информационной базы 1С
        public static Guid Subsystem = new Guid("37f2fa9a-b276-11d4-9435-004095e12fc7"); // Подсистемы
        public static Guid SharedProperty = new Guid("15794563-ccec-41f6-a83c-ec5f7b9a5bc1"); // Общие реквизиты
        public static Guid NamedDataTypeSet = new Guid("c045099e-13b9-4fb6-9d50-fca00202971e"); // Определяемые типы
        public static Guid Catalog = new Guid("cf4abea6-37b2-11d4-940f-008048da11f9"); // Справочники
        public static Guid Constant = new Guid("0195e80c-b157-11d4-9435-004095e12fc7"); // Константы
        public static Guid Document = new Guid("061d872a-5787-460e-95ac-ed74ea3a3e84"); // Документы
        public static Guid Enumeration = new Guid("f6a80749-5ad7-400b-8519-39dc5dff2542"); // Перечисления
        public static Guid Publication = new Guid("857c4a91-e5f4-4fac-86ec-787626f1c108"); // Планы обмена
        public static Guid Characteristic = new Guid("82a1b659-b220-4d94-a9bd-14d757b95a48"); // Планы видов характеристик
        public static Guid InformationRegister = new Guid("13134201-f60b-11d5-a3c7-0050bae0a776"); // Регистры сведений
        public static Guid AccumulationRegister = new Guid("b64d9a40-1642-11d6-a3c7-0050bae0a776"); // Регистры накопления

        public static List<Guid> AllSupportedTypes
        {
            get
            {
                return new List<Guid>()
                {
                    SharedProperty,
                    NamedDataTypeSet,
                    Catalog,
                    Document,
                    Enumeration,
                    Publication,
                    Characteristic,
                    InformationRegister,
                    AccumulationRegister
                };
            }
        }
        public static List<Guid> ApplicationObjectTypes
        {
            get
            {
                return new List<Guid>()
                {
                    Catalog,
                    Document,
                    Enumeration,
                    Publication,
                    Characteristic,
                    InformationRegister,
                    AccumulationRegister
                };
            }
        }
        public static List<Guid> ReferenceObjectTypes
        {
            get
            {
                return new List<Guid>()
                {
                    Catalog,
                    Document,
                    Enumeration,
                    Publication,
                    Characteristic
                };
            }
        }
        public static List<Guid> CatalogOwnerTypes
        {
            get
            {
                return new List<Guid>()
                {
                    Catalog,
                    Characteristic,
                    Publication
                };
            }
        }
        public static List<Guid> ValueObjectTypes
        {
            get
            {
                return new List<Guid>()
                {
                    InformationRegister,
                    AccumulationRegister
                };
            }
        }

        #region "RESOLVE BY NAME"

        #region " RU "

        private const string RU_CATALOG = "Справочник";
        private const string RU_DOCUMENT = "Документ";
        private const string RU_CONSTANT = "Константа";
        private const string RU_SUBSYSTEM = "Подсистема";
        private const string RU_PUBLICATION = "ПланОбмена";
        private const string RU_ENUMERATION = "Перечисление";
        private const string RU_CHARACTERISTIC = "ПланВидовХарактеристик";
        private const string RU_INFORMATION_REGISTER = "РегистрСведений";
        private const string RU_ACCUMULATION_REGISTER = "РегистрНакопления";
        private const string RU_SHARED_PROPERTY = "ОбщийРеквизит";
        private const string RU_NAMED_DATA_TYPE_SET = "ОпределяемыйТип";

        #endregion

        #region " EN "

        private const string EN_CATALOG = "Catalog";
        private const string EN_DOCUMENT = "Document";
        private const string EN_CONSTANT = "Constant";
        private const string EN_SUBSYSTEM = "Subsystem";
        private const string EN_PUBLICATION = "Publication";
        private const string EN_ENUMERATION = "Enumeration";
        private const string EN_CHARACTERISTIC = "Characteristic";
        private const string EN_INFORMATION_REGISTER = "InformationRegister";
        private const string EN_ACCUMULATION_REGISTER = "AccumulationRegister";
        private const string EN_SHARED_PROPERTY = "SharedProperty";
        private const string EN_NAMED_DATA_TYPE_SET = "NamedDataTypeSet";

        #endregion

        public static Guid ResolveName(in string name)
        {
            Guid uuid = ResolveNameRu(in name);

            if (uuid != Guid.Empty)
            {
                return uuid;
            }

            return ResolveNameEn(in name);
        }
        public static Guid ResolveNameRu(in string name)
        {
            if (name == RU_CATALOG) return Catalog;
            if (name == RU_DOCUMENT) return Document;
            if (name == RU_INFORMATION_REGISTER) return InformationRegister;
            if (name == RU_ACCUMULATION_REGISTER) return AccumulationRegister;

            if (name == RU_PUBLICATION) return Publication;
            if (name == RU_ENUMERATION) return Enumeration;
            if (name == RU_CHARACTERISTIC) return Characteristic;

            if (name == RU_CONSTANT) return Constant;
            if (name == RU_SUBSYSTEM) return Subsystem;

            if (name == RU_SHARED_PROPERTY) return SharedProperty;
            if (name == RU_NAMED_DATA_TYPE_SET) return NamedDataTypeSet;

            return Guid.Empty;
        }
        public static Guid ResolveNameEn(in string name)
        {
            if (name == EN_CATALOG) return Catalog;
            if (name == EN_DOCUMENT) return Document;
            if (name == EN_INFORMATION_REGISTER) return InformationRegister;
            if (name == EN_ACCUMULATION_REGISTER) return AccumulationRegister;

            if (name == EN_PUBLICATION) return Publication;
            if (name == EN_ENUMERATION) return Enumeration;
            if (name == EN_CHARACTERISTIC) return Characteristic;

            if (name == EN_CONSTANT) return Constant;
            if (name == EN_SUBSYSTEM) return Subsystem;

            if (name == EN_SHARED_PROPERTY) return SharedProperty;
            if (name == EN_NAMED_DATA_TYPE_SET) return NamedDataTypeSet;

            return Guid.Empty;
        }

        public static string ResolveName(Guid uuid)
        {
            string name = ResolveNameRu(uuid);

            if (name != string.Empty)
            {
                return name;
            }

            return ResolveNameEn(uuid);
        }
        public static string ResolveNameRu(Guid uuid)
        {
            if (uuid == Catalog) return RU_CATALOG;
            if (uuid == Document) return RU_DOCUMENT;
            if (uuid == InformationRegister) return RU_INFORMATION_REGISTER;
            if (uuid == AccumulationRegister) return RU_ACCUMULATION_REGISTER;

            if (uuid == Publication) return RU_PUBLICATION;
            if (uuid == Enumeration) return RU_ENUMERATION;
            if (uuid == Characteristic) return RU_CHARACTERISTIC;

            if (uuid == Constant) return RU_CONSTANT;
            if (uuid == Subsystem) return RU_SUBSYSTEM;

            if (uuid == SharedProperty) return RU_SHARED_PROPERTY;
            if (uuid == NamedDataTypeSet) return RU_NAMED_DATA_TYPE_SET;

            return string.Empty;
        }
        public static string ResolveNameEn(Guid uuid)
        {
            if (uuid == Catalog) return EN_CATALOG;
            if (uuid == Document) return EN_DOCUMENT;
            if (uuid == InformationRegister) return EN_INFORMATION_REGISTER;
            if (uuid == AccumulationRegister) return EN_ACCUMULATION_REGISTER;

            if (uuid == Publication) return EN_PUBLICATION;
            if (uuid == Enumeration) return EN_ENUMERATION;
            if (uuid == Characteristic) return EN_CHARACTERISTIC;

            if (uuid == Constant) return EN_CONSTANT;
            if (uuid == Subsystem) return EN_SUBSYSTEM;

            if (uuid == SharedProperty) return EN_SHARED_PROPERTY;
            if (uuid == NamedDataTypeSet) return EN_NAMED_DATA_TYPE_SET;

            return string.Empty;
        }

        #endregion
    }
}