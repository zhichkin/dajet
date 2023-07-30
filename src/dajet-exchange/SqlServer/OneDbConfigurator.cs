using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Options;
using System.Text;

namespace DaJet.Exchange.SqlServer
{
    public sealed class OneDbConfigurator : IOneDbConfigurator
    {
        private const string CREATE_SEQUENCE_SCRIPT =
            "IF NOT EXISTS(SELECT 1 FROM sys.sequences WHERE name = 'so_exchange_tuning') " +
            "BEGIN CREATE SEQUENCE so_exchange_tuning AS numeric(19,0) START WITH 1 INCREMENT BY 1; END;";
        private const string DELETE_SEQUENCE_SCRIPT =
            "IF EXISTS(SELECT 1 FROM sys.sequences WHERE name = 'so_exchange_tuning') " +
            "BEGIN DROP SEQUENCE so_exchange_tuning; END;";
        private const string EXISTS_TRIGGER_SCRIPT =
            "SELECT 1 FROM sys.triggers WHERE name = '{TRIGGER_NAME}';";
        private const string CREATE_TRIGGER_SCRIPT =
            "CREATE TRIGGER {TRIGGER_NAME} ON {TABLE_NAME} INSTEAD OF INSERT, UPDATE NOT FOR REPLICATION AS BEGIN " +
            "DECLARE @current_transaction_id bigint; " +
            "SELECT @current_transaction_id = transaction_id FROM sys.dm_tran_current_transaction; " +
            "INSERT {TARGET_NAME} " +
            "({Вектор}, {Узел_TYPE}, {Узел_TRef}, {Узел_RRef}, {Ссылка_TYPE}, {Ссылка_TRef}, {Ссылка_RRef}, {Транзакция}, {Данные}) " +
            "SELECT NEXT VALUE FOR so_exchange_tuning, 0x08, i._NodeTRef, i._NodeRRef, " +
            "{Дискриминатор}, {СсылкаТип}, {СсылкаЗначение}, @current_transaction_id, {ДанныеЗначение} FROM INSERTED AS i; " +
            "END;";
        private const string ENABLE_TRIGGER_SCRIPT =
            "ALTER TABLE {TABLE_NAME} ENABLE TRIGGER {TRIGGER_NAME};";
        private const string DELETE_TRIGGER_SCRIPT =
            "IF OBJECT_ID('{TRIGGER_NAME}', 'TR') IS NOT NULL BEGIN DROP TRIGGER {TRIGGER_NAME} END;";
        private const string DISABLE_TRIGGER_SCRIPT =
            "ALTER TABLE {TABLE_NAME} DISABLE TRIGGER {TRIGGER_NAME};";

        private List<MetadataObject> GetExchangeArticles(in Publication publication, in IMetadataProvider provider)
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
        private HashSet<MetadataObject> GetDistinctArticles(in IMetadataProvider provider)
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

        public void Configure(in IMetadataService metadata, in InfoBaseModel database)
        {
            if (!metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            IQueryExecutor executor = QueryExecutor.Create(DatabaseProvider.SqlServer, database.ConnectionString);

            MetadataObject register = provider.GetMetadataObject("РегистрСведений.РегистрацияИзменений");
            
            if (register is not InformationRegister target)
            {
                throw new InvalidOperationException("Target register is not found [РегистрСведений.РегистрацияИзменений].");
            }

            HashSet<MetadataObject> articles = GetDistinctArticles(in provider);

            if (articles.Count > 0)
            {
                CreateSequence(in executor);
            }

            foreach (MetadataObject article in articles)
            {
                CreateChangeTrackingTrigger(in executor, in provider, in article, in target);
            }
        }
        private string GetTableName(in ApplicationObject entity)
        {
            return entity.TableName;
        }
        private string GetTriggerName(in ApplicationObject entity)
        {
            return $"tr{entity.TableName}_exchange";
        }
        private ChangeTrackingTable GetChangeTrackingTable(in IMetadataProvider provider, in InformationRegister register)
        {
            MetadataObject article = provider.GetMetadataObject($"РегистрСведений.{register.Name}.Изменения");

            if (article is not ChangeTrackingTable table)
            {
                return null; // объект метаданных не включён ни в один план обмена
            }

            return table;
        }
        private Dictionary<string, string> GetColumnMap(in IMetadataProvider provider, in MetadataObject article, in InformationRegister target)
        {
            Dictionary<string, string> map = new();

            if (article is not ApplicationObject entity)
            {
                return map;
            }

            for (int i = 0; i < target.Properties.Count; i++)
            {
                MetadataProperty property = target.Properties[i];

                if (property.Name == "Вектор")
                {
                    foreach (MetadataColumn column in property.Columns)
                    {
                        if (column.Purpose == ColumnPurpose.Default)
                        {
                            map.Add("{Вектор}", column.Name); break;
                        }
                    }
                }
                else if (property.Name == "Узел")
                {
                    foreach (MetadataColumn column in property.Columns)
                    {
                        if (column.Purpose == ColumnPurpose.Tag)
                        {
                            map.Add("{Узел_TYPE}", column.Name);
                        }
                        else if (column.Purpose == ColumnPurpose.TypeCode)
                        {
                            map.Add("{Узел_TRef}", column.Name);
                        }
                        else if (column.Purpose == ColumnPurpose.Identity)
                        {
                            map.Add("{Узел_RRef}", column.Name);
                        }
                    }
                }
                else if (property.Name == "Ссылка")
                {
                    bool reference = (article is Catalog || article is Document);

                    foreach (MetadataColumn column in property.Columns)
                    {
                        if (column.Purpose == ColumnPurpose.Tag)
                        {
                            map.Add("{Ссылка_TYPE}", column.Name);
                            map.Add("{Дискриминатор}", reference ? "0x08" : "0x01");
                        }
                        else if (column.Purpose == ColumnPurpose.TypeCode)
                        {
                            map.Add("{Ссылка_TRef}", column.Name);
                            string typeCode = $"0x{Convert.ToHexString(DbUtilities.GetByteArray(entity.TypeCode)).ToLower()}";
                            map.Add("{СсылкаТип}", reference ? typeCode : "0x00000000");
                        }
                        else if (column.Purpose == ColumnPurpose.Identity)
                        {
                            map.Add("{Ссылка_RRef}", column.Name);
                            map.Add("{СсылкаЗначение}", reference ? "i._IDRRef" : "0x00000000000000000000000000000000");
                        }
                    }
                }
                else if (property.Name == "Транзакция")
                {
                    foreach (MetadataColumn column in property.Columns)
                    {
                        if (column.Purpose == ColumnPurpose.Default)
                        {
                            map.Add("{Транзакция}", column.Name); break;
                        }
                    }
                }
                else if (property.Name == "Данные")
                {
                    foreach (MetadataColumn column in property.Columns)
                    {
                        if (column.Purpose == ColumnPurpose.Default)
                        {
                            map.Add("{Данные}", column.Name); break;
                        }
                    }

                    if (article is Catalog)
                    {
                        map.Add("{ДанныеЗначение}", $"N'Справочник.{article.Name}'");
                    }
                    else if (article is Document)
                    {
                        map.Add("{ДанныеЗначение}", $"N'Документ.{article.Name}'");
                    }
                    else if (article is InformationRegister register)
                    {
                        ChangeTrackingTable table = GetChangeTrackingTable(in provider, in register);

                        if (table is null)
                        {
                            map.Add("{ДанныеЗначение}", $"N'Ошибка получения значений ключа для [РегистрСведений.{article.Name}]'");
                        }
                        else
                        {

                            StringBuilder value = new();
                            value.Append("(SELECT ");

                            int counter = 0;
                            for (int ii = 0; ii < table.Properties.Count; ii++)
                            {
                                MetadataProperty key = table.Properties[ii];

                                if (key.Name == "УзелОбмена" || key.Name == "НомерСообщения")
                                {
                                    continue;
                                }

                                foreach (MetadataColumn column in key.Columns)
                                {
                                    if (counter > 0) { value.Append(", "); }

                                    value.Append($"{column.Name} AS {key.Name}");

                                    counter++;
                                }
                            }

                            value.Append(" FROM INSERTED FOR XML RAW('key'), BINARY BASE64)");

                            map.Add("{ДанныеЗначение}", value.ToString());
                        }
                    }
                }
            }

            return map;
        }
        private void CreateSequence(in IQueryExecutor executor)
        {
            List<string> scripts = new()
            {
                CREATE_SEQUENCE_SCRIPT
            };
            executor.TxExecuteNonQuery(in scripts, 60);
        }
        private void CreateChangeTrackingTrigger(in IQueryExecutor executor, in IMetadataProvider provider, in MetadataObject article, in InformationRegister target)
        {
            if (provider is not MetadataCache cache) { return; }
            if (article is not ApplicationObject entity) { return; }
            ChangeTrackingTable table = cache.GetChangeTrackingTable(entity);
            if (table is null) { return; }

            string tableName = GetTableName(table);
            string targetName = GetTableName(target);
            string triggerName = GetTriggerName(in entity);

            string triggerExists = EXISTS_TRIGGER_SCRIPT.Replace("{TRIGGER_NAME}", triggerName);
            bool exists = (executor.ExecuteScalar<int>(in triggerExists, 10) == 1);
            if (exists) { return; }

            string createTrigger = CREATE_TRIGGER_SCRIPT
                .Replace("{TABLE_NAME}", tableName)
                .Replace("{TARGET_NAME}", targetName)
                .Replace("{TRIGGER_NAME}", triggerName);

            string enableTrigger = ENABLE_TRIGGER_SCRIPT
                .Replace("{TABLE_NAME}", tableName)
                .Replace("{TRIGGER_NAME}", triggerName);

            Dictionary<string, string> columnMap = GetColumnMap(in provider, in article, in target);

            foreach (var map in columnMap)
            {
                createTrigger = createTrigger.Replace(map.Key, map.Value);
            }
            
            List<string> scripts = new() { createTrigger, enableTrigger };

            executor.TxExecuteNonQuery(in scripts, 60);
        }
        
        public void Uninstall(in IMetadataService metadata, in InfoBaseModel database)
        {
            if (!metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            IQueryExecutor executor = QueryExecutor.Create(DatabaseProvider.SqlServer, database.ConnectionString);

            DeleteSequence(in executor);

            HashSet<MetadataObject> articles = GetDistinctArticles(in provider);

            foreach (MetadataObject article in articles)
            {
                DeleteChangeTrackingTrigger(in executor, in article);
            }
        }
        private void DeleteSequence(in IQueryExecutor executor)
        {
            List<string> scripts = new()
            {
                DELETE_SEQUENCE_SCRIPT
            };
            executor.TxExecuteNonQuery(in scripts, 60);
        }
        private void DeleteChangeTrackingTrigger(in IQueryExecutor executor, in MetadataObject article)
        {
            if (article is not ApplicationObject entity) { return; }

            string triggerName = GetTriggerName(in entity);

            string deleteTrigger = DELETE_TRIGGER_SCRIPT.Replace("{TRIGGER_NAME}", triggerName);

            List<string> scripts = new() { deleteTrigger };

            executor.TxExecuteNonQuery(in scripts, 60);
        }
    }
}