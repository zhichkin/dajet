using System;

namespace DaJet.Model
{
    [Entity(-20)]
    public sealed class PipelineRecord : EntityObject
    {
        private static readonly int MY_TYPE_CODE = -20;
        public PipelineRecord() : base(MY_TYPE_CODE) { }
        public PipelineRecord(Guid identity) : base(MY_TYPE_CODE, identity) { }
        public string Name { get; set; }
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }
    }
}