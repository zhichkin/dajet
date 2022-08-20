using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public interface IPredefinedValueOwner
    {
        List<PredefinedValue> PredefinedValues { get; set; }
    }
}