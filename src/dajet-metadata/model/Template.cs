namespace DaJet.Metadata.Model
{
    public sealed class Template : MetadataObject
    {
        public TemplateType Type { get; set; }
        public string GetFileName()
        {
            return this.Uuid.ToString().ToLower() + ".0";
        }
    }
}