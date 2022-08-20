namespace DaJet.Metadata.Model
{
    public interface IEntityHierarchy
    {
        bool IsHierarchical { get; set; }
        HierarchyType HierarchyType { get; set; }
    }
}