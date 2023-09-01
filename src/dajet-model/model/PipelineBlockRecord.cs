using System;

namespace DaJet.Model
{
    [Entity(-30)]
    public sealed class PipelineBlockRecord : EntityObject
    {
        private static readonly int MY_TYPE_CODE = -30;
        public PipelineBlockRecord() : base(MY_TYPE_CODE) { }
        public PipelineBlockRecord(Guid identity) : base(MY_TYPE_CODE, identity) { }
        public string Name { get; set; }
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }
    }
}