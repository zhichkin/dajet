using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;

namespace DaJet.Data
{
    public sealed class MsDbConfigurator : IDbConfigurator
    {
        private readonly IQueryExecutor _executor;
        public MsDbConfigurator(IQueryExecutor executor)
        {
            _executor = executor;
        }
        public bool TryCreateType(in TypeDefinition type)
        {
            throw new NotImplementedException();
        }
        private string[] GetIdentifiers(string metadataName)
        {
            if (string.IsNullOrWhiteSpace(metadataName))
            {
                throw new ArgumentNullException(nameof(metadataName));
            }

            string[] identifiers = metadataName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (identifiers.Length < 2)
            {
                throw new FormatException(nameof(metadataName));
            }

            return identifiers;
        }
        public TypeDefinition GetTypeDefinition(in string identifier)
        {
            string[] identifiers = GetIdentifiers(identifier);

            TypeDefinition type = new()
            {
                Name = "UDT",
                TableName = "_table"
            };

            type.Properties.Add(new MetadataProperty()
            {
                Name = "Code",
                Columns = new List<MetadataColumn>()
                {
                    new MetadataColumn() { Name = "_code" }
                },
                PropertyType = new DataTypeSet()
                {
                    CanBeString = true
                }
            });

            return type;
        }
    }
}