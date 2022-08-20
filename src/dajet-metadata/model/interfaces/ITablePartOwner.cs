using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public interface ITablePartOwner
    {
        List<TablePart> TableParts { get; set; }
    }
}