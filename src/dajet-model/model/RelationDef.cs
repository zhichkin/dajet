﻿using DaJet.Data;

namespace DaJet.Model
{
    public sealed class RelationDef
    {
        public Union Source { get; set; } // PropertyDef | UnionDef
        public Entity Target { get; set; } // TypeDef
    }
}