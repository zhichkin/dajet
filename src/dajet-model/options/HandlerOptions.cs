using System;
using System.Text.Json.Serialization;

namespace DaJet.Model
{
    public class HandlerOptions : OptionsBase
    {
        [JsonIgnore] public Guid Pipeline { get; set; } = Guid.Empty;
    }
}