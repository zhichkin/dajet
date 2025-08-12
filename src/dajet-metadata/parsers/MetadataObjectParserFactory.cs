using DaJet.Metadata.Core;
using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Parsers
{
    public sealed class MetadataObjectParserFactory
    {
        private readonly OneDbMetadataProvider _metadata;
        private readonly Dictionary<Guid, Func<IMetadataObjectParser>> _parsers;
        public MetadataObjectParserFactory(OneDbMetadataProvider metadata)
        {
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

            _parsers = new() // supported metadata object parsers
            {
                { MetadataTypes.Account, CreateAccountParser },
                { MetadataTypes.Catalog, CreateCatalogParser },
                { MetadataTypes.Document, CreateDocumentParser },
                { MetadataTypes.Constant, CreateConstantParser },
                { MetadataTypes.Enumeration, CreateEnumerationParser },
                { MetadataTypes.Publication, CreatePublicationParser },
                { MetadataTypes.Characteristic, CreateCharacteristicParser },
                { MetadataTypes.AccountingRegister, CreateAccountingRegisterParser },
                { MetadataTypes.InformationRegister, CreateInformationRegisterParser },
                { MetadataTypes.AccumulationRegister, CreateAccumulationRegisterParser },
                { MetadataTypes.SharedProperty, CreateSharedPropertyParser }, // since 1C:Enterprise 8.2.14 version
                { MetadataTypes.NamedDataTypeDescriptor, CreateNamedDataTypeDescriptorParser }, // since 1C:Enterprise 8.3.3 version
                { MetadataTypes.BusinessTask, CreateBusinessTaskParser },
                { MetadataTypes.BusinessProcess, CreateBusinessProcessParser }
            };
            // Включение режима совместимости с версией 8.2.13 и ниже несовместимо с наличием в конфигурации общих реквизитов
            // Использование определяемых типов в режиме совместимости 8.3.2 и ниже недопустимо
        }
        public bool TryCreateParser(Guid type, out IMetadataObjectParser parser)
        {
            parser = null;

            if (!_parsers.TryGetValue(type, out Func<IMetadataObjectParser> factory))
            {
                return false; // Unsupported metadata type
            }

            parser = factory();

            return true;
        }

        #region "CONCRETE PARSER FACTORIES"

        private IMetadataObjectParser CreateAccountParser()
        {
            return new AccountParser(_metadata);
        }
        private IMetadataObjectParser CreateCatalogParser()
        {
            return new CatalogParser(_metadata);
        }
        private IMetadataObjectParser CreateDocumentParser()
        {
            return new DocumentParser(_metadata);
        }
        private IMetadataObjectParser CreateConstantParser()
        {
            return new ConstantParser(_metadata);
        }
        private IMetadataObjectParser CreateEnumerationParser()
        {
            return new EnumerationParser(_metadata);
        }
        private IMetadataObjectParser CreatePublicationParser()
        {
            return new PublicationParser(_metadata);
        }
        private IMetadataObjectParser CreateCharacteristicParser()
        {
            return new CharacteristicParser(_metadata);
        }
        private IMetadataObjectParser CreateAccountingRegisterParser()
        {
            return new AccountingRegisterParser(_metadata);
        }
        private IMetadataObjectParser CreateInformationRegisterParser()
        {
            return new InformationRegisterParser(_metadata);
        }
        private IMetadataObjectParser CreateAccumulationRegisterParser()
        {
            return new AccumulationRegisterParser(_metadata);
        }
        private IMetadataObjectParser CreateSharedPropertyParser()
        {
            return new SharedPropertyParser(_metadata);
        }
        private IMetadataObjectParser CreateNamedDataTypeDescriptorParser()
        {
            return new NamedDataTypeDescriptorParser(_metadata);
        }
        private IMetadataObjectParser CreateBusinessTaskParser()
        {
            return new BusinessTaskParser(_metadata);
        }
        private IMetadataObjectParser CreateBusinessProcessParser()
        {
            return new BusinessProcessParser(_metadata);
        }

        #endregion
    }
}