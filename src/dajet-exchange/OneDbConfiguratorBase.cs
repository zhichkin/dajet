using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Options;

namespace DaJet.Exchange
{
    public interface IOneDbConfigurator
    {
        bool TryConfigure(in IMetadataService metadata, in InfoBaseModel database, out Dictionary<string, string> log);
        bool TryUninstall(in IMetadataService metadata, in InfoBaseModel database, out Dictionary<string, string> log);
    }
    public abstract class OneDbConfiguratorBase
    {
        public string GetTableName(in ApplicationObject entity)
        {
            return entity.TableName;
        }
        public string GetTriggerName(in ApplicationObject entity)
        {
            return $"tr{entity.TableName}_exchange";
        }
        public ChangeTrackingTable GetChangeTrackingTable(in IMetadataProvider provider, in InformationRegister register)
        {
            MetadataObject article = provider.GetMetadataObject($"РегистрСведений.{register.Name}.Изменения");

            if (article is not ChangeTrackingTable table)
            {
                return null; // объект метаданных не включён ни в один план обмена
            }

            return table;
        }
        public HashSet<MetadataObject> GetDistinctArticles(in IMetadataProvider provider)
        {
            HashSet<MetadataObject> distinct = new();

            foreach (MetadataItem item in provider.GetMetadataItems(MetadataTypes.Publication))
            {
                MetadataObject entity = provider.GetMetadataObject(item.Type, item.Uuid);

                if (entity is Publication publication)
                {
                    List<MetadataObject> articles = GetExchangeArticles(in publication, in provider);

                    foreach (MetadataObject article in articles)
                    {
                        _ = distinct.Add(article);
                    }
                }
            }

            return distinct;
        }
        public List<MetadataObject> GetExchangeArticles(in Publication publication, in IMetadataProvider provider)
        {
            List<MetadataObject> articles = new();

            List<Guid> types = new() { MetadataTypes.Catalog, MetadataTypes.Document, MetadataTypes.InformationRegister };

            foreach (Guid article in publication.Articles.Keys)
            {
                foreach (Guid type in types)
                {
                    MetadataObject entity = provider.GetMetadataObject(type, article);

                    if (entity is not null)
                    {
                        articles.Add(entity); break;
                    }
                }
            }

            return articles;
        }
    }
}