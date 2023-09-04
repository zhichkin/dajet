using System;

namespace DaJet.Model
{
    public sealed class PipelineBlockRecord : EntityObject
    {
        public string Name { get; set; }
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }
    }
}