﻿namespace DaJet.Metadata.Model
{
    public static class MetadataTokens
    {
        #region "Постфиксы наименования полей таблиц СУБД и идентификаторы типов данных"

        ///<summary>Boolean (config file metadata)</summary>
        public const string B = "B";
        ///<summary>Numeric</summary>
        public const string N = "N";
        ///<summary>String</summary>
        public const string S = "S";
        ///<summary>DateTime (config file metadata)</summary>
        public const string D = "D";
        ///<summary>Reference type, УникальныйИдентификатор или ХранилищеЗначения</summary>
        public const string R = "#";

        ///<summary>Поле таблицы СУБД - дискриминатор. Имеет постфикс _TYPE.</summary>
        public const string E = "E";
        ///<summary>Поле таблицы СУБД - версия данных. _Version (timestamp).</summary>
        public const string V = "V";

        //public const string R = "R"; - DbSchema - reference type field

        ///<summary>DateTime (SQL table fields postfix)</summary>
        public const string T = "T";
        ///<summary>Boolean (SQL table fields postfix)</summary>
        public const string L = "L";

        public const string RRef = "RRef";
        public const string TRef = "TRef";
        public const string RRRef = "RRRef";
        public const string RTRef = "RTRef";
        public const string TYPE = "TYPE"; // 0x08 - reference data type

        #endregion

        #region "Токены объектов метаданных"

        ///<summary>Табличная часть (вложенный значимый тип данных)</summary>
        public const string VT = "VT";
        ///<summary>Табличная часть "ВидыСубконто" счёта плана счетов</summary>
        public const string ExtDim = "ExtDim";
        ///<summary>Перечисление (ссылочный тип данных)</summary>
        public const string Enum = "Enum";
        ///<summary>План видов характеристик (ссылочный тип данных)</summary>
        public const string Chrc = "Chrc";
        ///<summary>Константа (значимый тип данных)</summary>
        public const string Const = "Const";
        ///<summary>Регистр сведений (значимый тип данных)</summary>
        public const string InfoRg = "InfoRg";
        ///<summary>Таблица настроек регистра сведений</summary>
        public const string InfoRgOpt = "InfoRgOpt";
        ///<summary>Таблица итогов (срез первых) периодического регистра сведений</summary>
        public const string InfoRgSF = "InfoRgSF";
        ///<summary>Таблица итогов (срез последних) периодического регистра сведений</summary>
        public const string InfoRgSL = "InfoRgSL";
        ///<summary>План счетов (ссылочный тип данных)</summary>
        public const string Acc = "Acc";
        ///<summary>Регистр бухгалтерии (значимый тип данных)</summary>
        public const string AccRg = "AccRg";
        ///<summary>Операции регистра бухгалтерии, журнал проводок (зависимый значимый тип данных)</summary>
        public const string AccRgED = "AccRgED";
        ///<summary>Регистр накопления (значимый тип данных)</summary>
        public const string AccumRg = "AccumRg";
        ///<summary>Таблица итогов регистра накопления остатков (зависимый значимый тип данных)</summary>
        public const string AccumRgT = "AccumRgT";
        ///<summary>Таблица итогов регистра накопления оборотов (зависимый значимый тип данных)</summary>
        public const string AccumRgTn = "AccumRgTn";
        ///<summary>Таблица настроек регистра накопления (зависимый значимый тип данных)</summary>
        public const string AccumRgOpt = "AccumRgOpt";
        ///<summary>Таблица изменений регистра накопления (зависимый значимый тип данных)</summary>
        public const string AccumRgChngR = "AccumRgChngR";
        ///<summary>Документ (ссылочный тип данных)</summary>
        public const string Document = "Document";
        ///<summary>Справочник (ссылочный тип данных)</summary>
        public const string Reference = "Reference";
        ///<summary>План обмена (ссылочный тип данных)</summary>
        public const string Node = "Node";
        ///<summary>Таблица изменений планов обмена (одна на каждый объект метаданных)</summary>
        public const string ChngR = "ChngR";
        ///<summary>Хранилище метаданных конфигурации 1С</summary>
        public const string Config = "Config";
        ///<summary>Бизнес-процесс</summary>
        public const string BPr = "BPr";
        ///<summary>Точки маршрута бизнес-процесса</summary>
        public const string BPrPoints = "BPrPoints";
        ///<summary>Задача бизнес-процесса</summary>
        public const string Task = "Task";

        #endregion

        #region "Токены полей таблиц СУБД объектов метаданных"

        public const string Fld = "Fld";
        public const string IDRRef = "IDRRef";
        public const string Version = "Version";
        public const string Marked = "Marked";
        public const string DateTime = "Date_Time";
        public const string NumberPrefix = "NumberPrefix";
        public const string Number = "Number";
        public const string Posted = "Posted";
        public const string PredefinedID = "PredefinedID";
        public const string Description = "Description";
        public const string Code = "Code";
        public const string OwnerID = "OwnerID";
        public const string Folder = "Folder";
        public const string ParentIDRRef = "ParentIDRRef";

        public const string KeyField = "KeyField";
        public const string LineNo = "LineNo";
        public const string EnumOrder = "EnumOrder";
        public const string Type = "Type"; // ТипЗначения (ПланВидовХарактеристик)

        public const string Kind = "Kind"; // Тип счёта плана счетов (активный, пассивный, активно-пассивный)
        public const string OrderField = "OrderField"; // Порядок счёта в плане счетов
        public const string OffBalance = "OffBalance"; // Признак забалансового счёта плана счетов
        public const string AccountDtRRef = "AccountDtRRef"; // Cчёт по дебету проводки регистра бухгалтерского учёта
        public const string AccountCtRRef = "AccountCtRRef"; // Cчёт по кредиту проводки регистра бухгалтерского учёта
        public const string EDHashDt = "EDHashDt"; // Хэш проводки по дебету регистра бухгалтерского учёта
        public const string EDHashCt = "EDHashCt"; // Хэш проводки по кредиту регистра бухгалтерского учёта
        public const string Period = "Period";
        public const string Periodicity = "Periodicity";
        public const string ActualPeriod = "ActualPeriod";
        public const string Recorder = "Recorder";
        public const string RecorderRRef = "RecorderRRef";
        public const string RecorderTRef = "RecorderTRef";
        public const string Active = "Active";
        public const string RecordKind = "RecordKind";
        public const string SentNo = "SentNo";
        public const string ReceivedNo = "ReceivedNo";

        public const string Splitter = "Splitter";
        public const string NodeTRef = "NodeTRef";
        public const string NodeRRef = "NodeRRef";
        public const string MessageNo = "MessageNo";
        public const string UseTotals = "UseTotals";
        public const string UseSplitter = "UseSplitter";
        public const string MinPeriod = "MinPeriod";
        public const string MinCalculatedPeriod = "MinCalculatedPeriod";
        public const string RepetitionFactor = "RepetitionFactor";

        #endregion
    }
}