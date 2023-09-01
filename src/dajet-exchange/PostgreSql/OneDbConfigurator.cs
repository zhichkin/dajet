using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Model;
using System.Text;

namespace DaJet.Exchange.PostgreSql
{
    public sealed class OneDbConfigurator : OneDbConfiguratorBase, IOneDbConfigurator
    {
        private const string CREATE_SEQUENCE_SCRIPT =
            "CREATE SEQUENCE IF NOT EXISTS so_exchange_tuning AS bigint INCREMENT BY 1 START WITH 1 CACHE 1;";
        private const string DELETE_SEQUENCE_SCRIPT =
            "DROP SEQUENCE IF EXISTS so_exchange_tuning;";
        private const string DATA_MIGRATION_SCRIPT =
            "LOCK TABLE {TABLE_NAME} IN ACCESS EXCLUSIVE MODE; " +
            "WITH deleted AS (DELETE FROM {TABLE_NAME} RETURNING _NodeTRef, _NodeRRef, {КлючЗаписи}) " +
            "INSERT INTO {TARGET_NAME} " +
            "({Вектор}, {Узел_TYPE}, {Узел_TRef}, {Узел_RRef}, {Ссылка_TYPE}, {Ссылка_TRef}, {Ссылка_RRef}, {Транзакция}, {Данные}) " +
            "SELECT CAST(nextval('so_exchange_tuning') AS numeric(19,0)), '\\\\x08'::bytea, _NodeTRef, _NodeRRef, " +
            "{Дискриминатор}, {СсылкаТип}, {DELETE.СсылкаЗначение}, 0, {DELETE.ДанныеЗначение} FROM deleted;";
        private const string EXISTS_TRIGGER_SCRIPT =
            "SELECT 1 FROM information_schema.triggers WHERE LOWER(trigger_name) = LOWER('{TRIGGER_NAME}');";
        private const string CREATE_FUNCTION_SCRIPT =
            "CREATE OR REPLACE FUNCTION fn{TABLE_NAME}_exchange() RETURNS trigger AS $$ BEGIN " +
            "IF NOT NEW._messageno IS NULL THEN RETURN NEW; END IF; " + // Это вызов ПланыОбмена.ВыбратьИзменения(...) - продолжаем выполнение команды 1С
            "INSERT INTO {TARGET_NAME} " +
            "({Вектор}, {Узел_TYPE}, {Узел_TRef}, {Узел_RRef}, {Ссылка_TYPE}, {Ссылка_TRef}, {Ссылка_RRef}, {Транзакция}, {Данные}) " +
            "SELECT CAST(nextval('so_exchange_tuning') AS numeric(19,0)), '\\\\x08'::bytea, NEW._NodeTRef, NEW._NodeRRef, " +
            "{Дискриминатор}, {СсылкаТип}, {INSERT.СсылкаЗначение}, CAST(txid_current() AS numeric(19,0)), {INSERT.ДанныеЗначение}; " +
            "RETURN NULL; " + // -- Отменяем выполнение команды 1С
            "END; $$ LANGUAGE 'plpgsql';";
        private const string DELETE_FUNCTION_SCRIPT =
            "DROP FUNCTION IF EXISTS fn{TABLE_NAME}_exchange();";
        private const string CREATE_TRIGGER_SCRIPT =
            "CREATE TRIGGER {TRIGGER_NAME} BEFORE INSERT OR UPDATE ON {TABLE_NAME} " +
            "FOR EACH ROW EXECUTE FUNCTION fn{TABLE_NAME}_exchange();";
        private const string DELETE_TRIGGER_SCRIPT =
            "DROP TRIGGER IF EXISTS {TRIGGER_NAME} ON {TABLE_NAME};";
        private const string ENABLE_TRIGGER_SCRIPT =
            "ALTER TABLE IF EXISTS {TABLE_NAME} ENABLE TRIGGER {TRIGGER_NAME};";
        private const string DISABLE_TRIGGER_SCRIPT =
            "ALTER TABLE IF EXISTS {TABLE_NAME} DISABLE TRIGGER {TRIGGER_NAME};";
        public bool TryConfigure(in IMetadataService metadata, in InfoBaseRecord database, out Dictionary<string, string> log)
        {
            log = new Dictionary<string, string>();

            if (!metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                log.Add($"Database [{database.Name}] is not accessible", error); return false;
            }

            IQueryExecutor executor = QueryExecutor.Create(DatabaseProvider.PostgreSql, database.ConnectionString);

            MetadataObject register = provider.GetMetadataObject("РегистрСведений.РегистрацияИзменений");

            if (register is not InformationRegister target)
            {
                log.Add("Target register is not found", "РегистрСведений.РегистрацияИзменений"); return false;
            }

            HashSet<MetadataObject> articles = GetDistinctArticles(in provider);

            if (articles.Count > 0)
            {
                try
                {
                    CreateSequence(in executor);
                }
                catch (Exception exception)
                {
                    log.Add("Create sequence error", ExceptionHelper.GetErrorMessage(exception)); return false;
                }
            }

            bool success = true;

            foreach (MetadataObject article in articles)
            {
                string metadataName = article.ToString();

                try
                {
                    CreateChangeTrackingTrigger(in executor, in provider, in article, in target);

                    _ = log.TryAdd(metadataName, "CREATED");
                }
                catch (Exception exception)
                {
                    success = false;

                    _ = log.TryAdd(metadataName, ExceptionHelper.GetErrorMessage(exception));
                }
            }

            return success;
        }
        private void CreateSequence(in IQueryExecutor executor)
        {
            List<string> scripts = new()
            {
                CREATE_SEQUENCE_SCRIPT
            };
            executor.TxExecuteNonQuery(in scripts, 60);
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
                            map.Add("{Дискриминатор}", reference ? "'\\\\x08'::bytea" : "'\\\\x01'::bytea");
                        }
                        else if (column.Purpose == ColumnPurpose.TypeCode)
                        {
                            map.Add("{Ссылка_TRef}", column.Name);
                            string typeCode = $"'\\\\x{Convert.ToHexString(DbUtilities.GetByteArray(entity.TypeCode)).ToLower()}'::bytea";
                            map.Add("{СсылкаТип}", reference ? typeCode : "'\\\\x00000000'::bytea");
                        }
                        else if (column.Purpose == ColumnPurpose.Identity)
                        {
                            map.Add("{Ссылка_RRef}", column.Name);
                            map.Add("{DELETE.СсылкаЗначение}", reference ? "_IDRRef" : "'\\\\x00000000000000000000000000000000'::bytea");
                            map.Add("{INSERT.СсылкаЗначение}", reference ? "NEW._IDRRef" : "'\\\\x00000000000000000000000000000000'::bytea");
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

                    if (article is Catalog) // CAST('Справочник.Номенклатура' AS mvarchar)
                    {
                        map.Add("{КлючЗаписи}", "_IDRRef");
                        map.Add("{DELETE.ДанныеЗначение}", $"'Справочник.{article.Name}'");
                        map.Add("{INSERT.ДанныеЗначение}", $"'Справочник.{article.Name}'");
                    }
                    else if (article is Document)
                    {
                        map.Add("{КлючЗаписи}", "_IDRRef");
                        map.Add("{DELETE.ДанныеЗначение}", $"'Документ.{article.Name}'");
                        map.Add("{INSERT.ДанныеЗначение}", $"'Документ.{article.Name}'");
                    }
                    else if (article is InformationRegister register)
                    {
                        ChangeTrackingTable table = GetChangeTrackingTable(in provider, in register);

                        if (table is null)
                        {
                            throw new InvalidOperationException($"Ошибка определения ключа для [РегистрСведений.{article.Name}]");
                        }
                        else
                        {
                            StringBuilder keyColumns = new();
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
                                    if (counter > 0) { keyColumns.Append(", "); }
                                    keyColumns.Append($"{column.Name}");
                                    counter++;
                                }
                            }
                            map.Add("{КлючЗаписи}", keyColumns.ToString());

                            map.Add("{DELETE.ДанныеЗначение}", "row_to_json(deleted)::text::mvarchar");
                            map.Add("{INSERT.ДанныеЗначение}", "row_to_json(NEW)::text::mvarchar");
                        }
                    }
                }
            }

            return map;
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

            string dataMigration = DATA_MIGRATION_SCRIPT
                .Replace("{TABLE_NAME}", tableName)
                .Replace("{TARGET_NAME}", targetName);

            string createFunction = CREATE_FUNCTION_SCRIPT
                .Replace("{TABLE_NAME}", tableName)
                .Replace("{TARGET_NAME}", targetName);

            string createTrigger = CREATE_TRIGGER_SCRIPT
                .Replace("{TABLE_NAME}", tableName)
                .Replace("{TRIGGER_NAME}", triggerName);

            string enableTrigger = ENABLE_TRIGGER_SCRIPT
                .Replace("{TABLE_NAME}", tableName)
                .Replace("{TRIGGER_NAME}", triggerName);

            Dictionary<string, string> columnMap = GetColumnMap(in provider, in article, in target);

            foreach (var map in columnMap)
            {
                dataMigration = dataMigration.Replace(map.Key, map.Value);
                createTrigger = createTrigger.Replace(map.Key, map.Value);
                createFunction = createFunction.Replace(map.Key, map.Value);
            }

            List<string> scripts = new() { dataMigration, createFunction, createTrigger, enableTrigger };

            executor.TxExecuteNonQuery(in scripts, 60);
        }
        
        public bool TryUninstall(in IMetadataService metadata, in InfoBaseRecord database, out Dictionary<string, string> log)
        {
            log = new Dictionary<string, string>();

            if (!metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                log.Add($"Database [{database.Name}] is not accessible", error); return false;
            }

            IQueryExecutor executor = QueryExecutor.Create(DatabaseProvider.PostgreSql, database.ConnectionString);

            HashSet<MetadataObject> articles = GetDistinctArticles(in provider);

            bool success = true;

            foreach (MetadataObject article in articles)
            {
                string metadataName = article.ToString();

                try
                {
                    DeleteChangeTrackingTrigger(in provider, in executor, in article);

                    _ = log.TryAdd(metadataName, "DROPPED");
                }
                catch (Exception exception)
                {
                    success = false;

                    _ = log.TryAdd(metadataName, ExceptionHelper.GetErrorMessage(exception));
                }
            }

            if (!success)
            {
                return false; // do not drop sequence !
            }

            try
            {
                DeleteSequence(in executor);
            }
            catch (Exception exception)
            {
                log.Add($"Create sequence error", ExceptionHelper.GetErrorMessage(exception)); return false;
            }

            return success;
        }
        private void DeleteSequence(in IQueryExecutor executor)
        {
            List<string> scripts = new()
            {
                DELETE_SEQUENCE_SCRIPT
            };
            executor.TxExecuteNonQuery(in scripts, 60);
        }
        private void DeleteChangeTrackingTrigger(in IMetadataProvider provider, in IQueryExecutor executor, in MetadataObject article)
        {
            if (provider is not MetadataCache cache) { return; }
            if (article is not ApplicationObject entity) { return; }
            ChangeTrackingTable table = cache.GetChangeTrackingTable(entity);
            if (table is null) { return; }

            string tableName = GetTableName(table);
            string triggerName = GetTriggerName(in entity);

            string deleteTrigger = DELETE_TRIGGER_SCRIPT
                .Replace("{TABLE_NAME}", tableName)
                .Replace("{TRIGGER_NAME}", triggerName);

            string deleteFunction = DELETE_FUNCTION_SCRIPT
                .Replace("{TABLE_NAME}", tableName);

            List<string> scripts = new() { deleteTrigger, deleteFunction };

            executor.TxExecuteNonQuery(in scripts, 60);
        }
    }
}