using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public sealed class Constant : ApplicationObject
    {
        public DataTypeDescriptor DataTypeDescriptor { get; set; }
        public DataTypeDescriptor ExtensionDataTypeDescriptor { get; set; }
        /// <summary>
        /// Определяет логическую ссылочную целостность базы данных <see cref="MetadataProperty.References"/>
        /// </summary>
        public List<Guid> References { get; } = new();
    }
    
    //PropertyNameLookup.Add("_recordkey", "КлючЗаписи"); // binary(1)
}