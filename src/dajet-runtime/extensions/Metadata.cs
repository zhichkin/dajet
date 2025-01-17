using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;

namespace DaJet.Runtime
{
    public sealed class Metadata : UserDefinedProcessor
    {
        public Metadata(in ScriptScope scope) : base(scope) { }
        public override void Process() { Execute(); _next?.Process(); }
        private void Execute()
        {
            _ = _scope.TryGetValue(_statement.Variables[0].Identifier, out object value);

            if (value is not string metadataName)
            {
                throw new ArgumentException($"[{nameof(Metadata)}] metadata name missing");
            }

            if (!_scope.TryGetMetadataProvider(out IMetadataProvider metadata, out string error))
            {
                throw new InvalidOperationException(error);
            }

            MetadataObject @object = metadata.GetMetadataObject(metadataName);

            object returnValue = null;

            if (@object is not null)
            {
                returnValue = @object.ToDataObject();
            }
            else if (metadataName == "Метаданные.Объекты")
            {
                returnValue = ReturnMetadataObjects(in metadata);
            }
            else if (metadataName.StartsWith("ПланОбмена") && metadataName.EndsWith("Состав"))
            {
                returnValue = ReturnPublicationArticles(in metadata, in metadataName);
            }
            else
            {
                throw new InvalidOperationException($"[{nameof(Metadata)}] {{{metadataName}}} unsupported");
            }
            
            SetReturnValue(in returnValue);
        }
        private void SetReturnValue(in object value)
        {
            if (!_scope.TrySetValue(_statement.Return.Identifier, value))
            {
                throw new InvalidOperationException($"Failed to set return variable {_statement.Return.Identifier}");
            }
        }
        private static void GetMetadataObjects(Guid type, in IMetadataProvider provider, in List<DataObject> table)
        {
            foreach (MetadataItem item in provider.GetMetadataItems(type))
            {
                MetadataObject metadata = provider.GetMetadataObject(item.Type, item.Uuid);

                DataObject record = metadata.ToDataObject();

                table.Add(record);

                if (metadata is ApplicationObject entity)
                {
                    //if (entity is ITablePartOwner owner)
                    //{
                    //    foreach (TablePart tablePart in owner.TableParts)
                    //    {
                    //        record = new Dictionary<string, object>()
                    //        {
                    //            { "Ссылка",   tablePart.Uuid },
                    //            { "Код",      new decimal(tablePart.TypeCode) },
                    //            { "Тип",      "ТабличнаяЧасть" },
                    //            { "Имя",      tablePart.Name },
                    //            { "Таблица",  tablePart.TableName },
                    //            { "Владелец", entity.Uuid }
                    //        };
                    //        table.Add(record);
                    //    }
                    //}
                }
            }
        }
        private List<DataObject> ReturnMetadataObjects(in IMetadataProvider metadata)
        {
            List<DataObject> table = new();

            GetMetadataObjects(MetadataTypes.Catalog, in metadata, in table);
            GetMetadataObjects(MetadataTypes.Document, in metadata, in table);
            GetMetadataObjects(MetadataTypes.InformationRegister, in metadata, in table);
            GetMetadataObjects(MetadataTypes.AccumulationRegister, in metadata, in table);

            return table;
        }
        private List<DataObject> ReturnPublicationArticles(in IMetadataProvider provider, in string metadataName)
        {
            List<DataObject> articles = new();

            string[] identifiers = metadataName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            string identifier = $"{identifiers[0]}.{identifiers[1]}";

            MetadataObject metadata = provider.GetMetadataObject(identifier);

            List<Guid> types = new();
            types.AddRange(MetadataTypes.ReferenceObjectTypes);
            types.AddRange(MetadataTypes.ValueObjectTypes);

            if (metadata is Publication publication)
            {
                foreach (Guid uuid in publication.Articles.Keys)
                {
                    foreach (Guid type in types)
                    {
                        MetadataObject article = provider.GetMetadataObject(type, uuid);

                        if (article is not null)
                        {
                            articles.Add(article.ToDataObject()); break;
                        }
                    }
                }
            }

            return articles;
        }

        private void ResolveReferencesToMetadataItems(in IMetadataProvider provider, in List<Guid> references)
        {
            if (provider is not OneDbMetadataProvider metadata) { return; }
            
            List<MetadataItem> list = metadata.ResolveReferencesToMetadataItems(in references);
        }
        private void ResolveReferencesРодитель(in IMetadataProvider provider)
        {
            if (provider is not OneDbMetadataProvider metadata) { return; }

            ApplicationObject catalog = new Catalog();

            MetadataProperty property = catalog.Properties
                .Where(p => p.Purpose == PropertyPurpose.System && p.Name == "Родитель")
                .FirstOrDefault();

            Guid type = Guid.Empty;

            if (catalog is Account)
            {
                type = MetadataTypes.Account;
            }
            else if (catalog is Catalog)
            {
                type = MetadataTypes.Catalog;
            }
            else if (catalog is Characteristic)
            {
                type = MetadataTypes.Characteristic;
            }
            else
            {
                return;
            }
            
            if (metadata.ResolveReferences)
            {
                Guid uuid = catalog.Uuid; // Родитель всегда одного и того же типа, что и объект
            }
        }
        private void ResolveReferencesВладелец(in IMetadataProvider provider)
        {
            if (provider is not OneDbMetadataProvider metadata) { return; }

            ApplicationObject catalog = new Catalog();

            MetadataProperty property = catalog.Properties
                .Where(p => p.Purpose == PropertyPurpose.System && p.Name == "Родитель")
                .FirstOrDefault();

            List<Guid> owners = metadata.GetCatalogOwners(catalog.Uuid);

            if (owners is not null && owners.Count > 0)
            {
                if (metadata.ResolveReferences)
                {
                    foreach (Guid owner in owners)
                    {
                        MetadataItem item = metadata.GetCatalogOwner(owner);
                    }
                }
            }
        }
        private void ResolveReferencesРегистратор(in IMetadataProvider provider)
        {
            if (provider is not OneDbMetadataProvider metadata) { return; }

            ApplicationObject register = new InformationRegister();

            MetadataProperty property = register.Properties
                .Where(p => p.Purpose == PropertyPurpose.System && p.Name == "Регистратор")
                .FirstOrDefault();

            List<Guid> recorders = metadata.GetRegisterRecorders(register.Uuid);

            if (recorders is null || recorders.Count == 0) { return; }

            if (metadata.ResolveReferences)
            {
                foreach (Guid recorder in property.PropertyType.References)
                {
                    MetadataItem item = metadata.GetRegisterRecorder(recorder);
                }
            }
        }
    }
}