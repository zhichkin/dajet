﻿using DaJet.Model;
using System.ComponentModel.DataAnnotations;

namespace DaJet.Flow.SqlServer
{
    public sealed class ConsumerOptions : OptionsBase
    {
        [Required] public string Source { get; set; } = string.Empty;
        [Required] public string Script { get; set; } = string.Empty;
        public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
    }
}