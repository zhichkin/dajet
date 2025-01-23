using System.Collections.Generic;
using System;

namespace DaJet.Metadata.Model
{
    public sealed class NamedDataTypeDescriptor : MetadataObject
    {
        public DataTypeDescriptor DataTypeDescriptor { get; set; }
        public DataTypeDescriptor ExtensionDataTypeDescriptor { get; set; }
        /// <summary>
        /// Определяет логическую ссылочную целостность базы данных <see cref="MetadataProperty.References"/>
        /// </summary>
        public List<Guid> References { get; } = [];
    }
}