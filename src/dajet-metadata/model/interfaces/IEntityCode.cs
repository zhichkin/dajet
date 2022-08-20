namespace DaJet.Metadata.Model
{
    public interface IEntityCode
    {
        int CodeLength { get; set; }
        CodeType CodeType { get; set; }
    }
}