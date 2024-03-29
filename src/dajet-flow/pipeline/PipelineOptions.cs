﻿using DaJet.Model;

namespace DaJet.Flow
{
    public sealed class PipelineOptions : OptionsBase
    {
        public Guid Uuid { get; set; } = Guid.Empty;
        public string Name { get; set; } = string.Empty;
        public ActivationMode Activation { get; set; } = ActivationMode.Manual;
        public int SleepTimeout { get; set; } = 0; // seconds (0 - run once)
        public bool ShowStackTrace { get; set; } = false;
    }
}