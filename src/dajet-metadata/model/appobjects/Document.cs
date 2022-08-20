using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public sealed class Document : ApplicationObject, ITablePartOwner
    {
        public int NumberLength { get; set; } = 8;
        public NumberType NumberType { get; set; } = NumberType.String;
        public Periodicity Periodicity { get; set; } = Periodicity.None;
        public List<TablePart> TableParts { get; set; } = new List<TablePart>();
    }
    
    //PropertyNameLookup.Add("_idrref", "Ссылка");
    //PropertyNameLookup.Add("_version", "ВерсияДанных");
    //PropertyNameLookup.Add("_marked", "ПометкаУдаления");
    //PropertyNameLookup.Add("_date_time", "Дата");
    //PropertyNameLookup.Add("_number", "Номер"); // необязательный
    //PropertyNameLookup.Add("_posted", "Проведён");
}