using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public sealed class Publication : ApplicationObject,
        IEntityCode, IEntityDescription, ITablePartOwner
    {
        public int CodeLength { get; set; } = 9; // min 1
        public CodeType CodeType { get; set; } = CodeType.String; // always
        public int DescriptionLength { get; set; } = 25; // min 1
        public bool IsDistributed { get; set; }
        public List<TablePart> TableParts { get; set; } = new List<TablePart>();
        public Publisher Publisher { get; set; } // ЭтотУзел (предопределённый)
        public List<Subscriber> Subscribers { get; set; } = new List<Subscriber>(); // Узлы подписчики
        /// <summary>
        /// Состав плана обмена. Ключ словаря - идентификатор файла объекта метаданных (<see cref="MetadataObject.Uuid"/>).
        /// </summary>
        public Dictionary<Guid, AutoPublication> Articles { get; set; } = new Dictionary<Guid, AutoPublication>();
    }
    public sealed class Publisher
    {
        public Guid Uuid { get; set; } = Guid.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
    public sealed class Subscriber
    {
        public Guid Uuid { get; set; } = Guid.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsMarkedForDeletion { get; set; }
    }

    // все реквизиты обязательные
    //PropertyNameLookup.Add("_idrref", "Ссылка");
    //PropertyNameLookup.Add("_marked", "ПометкаУдаления");
    //PropertyNameLookup.Add("_version", "ВерсияДанных");
    //PropertyNameLookup.Add("_predefinedid", "Предопределённый");
    //PropertyNameLookup.Add("_code", "Код");
    //PropertyNameLookup.Add("_description", "Наименование");
    //PropertyNameLookup.Add("_sentno", "НомерОтправленного");
    //PropertyNameLookup.Add("_receivedno", "НомерПринятого");
}