using System;

namespace DaJet.Model
{
    public sealed class PipelineState
    {
        public Guid Uuid { get; init; } = Guid.Empty;
        public string Name { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime Start { get; init; } = DateTime.MinValue;
        public DateTime Finish { get; init; } = DateTime.MinValue;
        public string State { get; init; } = "Idle";
        public string Activation { get; init; } = "Manual";
    }
}