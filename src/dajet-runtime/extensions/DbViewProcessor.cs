using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Services;
using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class DbViewProcessor : UserDefinedProcessor
    {
        private string _command;
        private IDbConnectionFactory _factory;
        private OneDbMetadataProvider _provider;
        public DbViewProcessor(in ScriptScope scope) : base(scope) { }
        public override void Process()
        {
            _command = GetCommandName();

            Initialize();

            if (_command == "SELECT SCHEMAS")
            {
                foreach (string schema in SelectDatabaseSchemas())
                {
                    SetReturnValue(schema);

                    _next?.Process();
                }
            }
            else if (_command == "CREATE SCHEMA")
            {
                CreateDatabaseSchema();
                SetReturnValue(true);
            }
            else if (_command == "DROP SCHEMA")
            {
                DeleteDatabaseSchema();
                SetReturnValue(true);
            }
            else if (_command == "SELECT VIEWS")
            {

            }
            else if (_command == "SCRIPT VIEWS")
            {

            }
            else if (_command == "SCRIPT VIEW")
            {

            }
            else if (_command == "CREATE VIEWS")
            {

            }
            else if (_command == "CREATE VIEW")
            {
                // single object
            }
            else if (_command == "DROP VIEWS")
            {

            }
            else if (_command == "DROP VIEW")
            {
                // single object
            }
            else
            {
                
            }

            SetReturnValue(null);
        }
        private string GetCommandName()
        {
            _ = _scope.TryGetValue(_statement.Variables[0].Identifier, out object value);

            if (value is not string command)
            {
                throw new ArgumentException($"[{nameof(MetadataStreamer)}] command name is missing");
            }

            return command;
        }
        private string GetDatabaseSchemaName()
        {
            foreach (ColumnExpression option in _statement.Options)
            {
                if (option.Alias == "SchemaName")
                {
                    if (StreamFactory.TryEvaluate(in _scope, option.Expression, out object value)
                        && value is string schemaName
                        && !string.IsNullOrWhiteSpace(schemaName))
                    {
                        return schemaName;
                    }
                }
            }

            throw new InvalidOperationException($"[{nameof(DbViewProcessor)}] option \"SchemaName\" is not defined");
        }
        private void Initialize()
        {
            if (_provider is not null) { return; }

            Uri uri = _scope.GetDatabaseUri(); //_scope.GetUri(source);

            _factory = DbConnectionFactory.GetFactory(in uri);

            OneDbMetadataProviderOptions options = new()
            {
                UseExtensions = false,
                ResolveReferences = true,
                ConnectionString = DbConnectionFactory.GetConnectionString(in uri)
            };

            if (uri.Scheme == "mssql")
            {
                options.DatabaseProvider = DatabaseProvider.SqlServer;
            }
            else if (uri.Scheme == "pgsql")
            {
                options.DatabaseProvider = DatabaseProvider.PostgreSql;
            }
            else
            {
                throw new NotSupportedException($"[{nameof(MetadataStreamer)}] database {uri.Scheme} is not supported");
            }

            if (!OneDbMetadataProvider.TryCreateMetadataProvider(in options, out _provider, out string error))
            {
                FileLogger.Default.Write(error);
            }
        }
        private IEnumerable<string> SelectDatabaseSchemas()
        {
            DbViewGeneratorOptions options = new()
            {
                DatabaseProvider = _provider.DatabaseProvider,
                ConnectionString = _provider.ConnectionString
            };

            IDbViewGenerator generator = DaJet.Metadata.Services.DbViewGenerator.Create(options);

            foreach (string schema in generator.SelectSchemas())
            {
                yield return schema;
            }
        }
        private void CreateDatabaseSchema()
        {
            string schema = GetDatabaseSchemaName();

            DbViewGeneratorOptions options = new()
            {
                DatabaseProvider = _provider.DatabaseProvider,
                ConnectionString = _provider.ConnectionString
            };

            IDbViewGenerator generator = DaJet.Metadata.Services.DbViewGenerator.Create(options);

            generator.CreateSchema(schema);
        }
        private void DeleteDatabaseSchema()
        {
            string schema = GetDatabaseSchemaName();

            DbViewGeneratorOptions options = new()
            {
                DatabaseProvider = _provider.DatabaseProvider,
                ConnectionString = _provider.ConnectionString
            };

            IDbViewGenerator generator = DaJet.Metadata.Services.DbViewGenerator.Create(options);

            generator.DropSchema(schema);
        }
        private void SetReturnValue(in object value)
        {
            if (!_scope.TrySetValue(_statement.Return.Identifier, value))
            {
                throw new InvalidOperationException($"Error setting return variable {_statement.Return.Identifier}");
            }
        }
        
        //*********

        private void CheckMetadataAgainstDatabaseSchema()
        {
            foreach (Guid type in MetadataTypes.ApplicationObjectTypes)
            {
                foreach (MetadataItem item in _provider.GetMetadataItems(type))
                {
                    MetadataObject metadata = _provider.GetMetadataObject(item.Type, item.Uuid);

                    if (metadata is ApplicationObject entity)
                    {
                        string entityName = entity.Name;
                        PerformDatabaseSchemaCheck(in entityName, entity.TableName, entity.Properties);

                        if (entity is ITablePartOwner owner)
                        {
                            foreach (TablePart table in owner.TableParts)
                            {
                                string tableName = $"{entityName}.{table.Name}";
                                PerformDatabaseSchemaCheck(in tableName, table.TableName, table.Properties);
                            }
                        }
                    }
                }
            }
        }
        private void PerformDatabaseSchemaCheck(in string entityName, in string tableName, in List<MetadataProperty> properties)
        {
            SqlMetadataReader sql = new();
            sql.UseDatabaseProvider(_provider.DatabaseProvider);
            sql.UseConnectionString(_provider.ConnectionString);
            
            MetadataCompareAndMergeService comparator = new();

            List<DaJet.Metadata.Services.SqlFieldInfo> fields = sql.GetSqlFieldsOrderedByName(tableName);

            List<string> source = comparator.PrepareComparison(fields); // эталон (как должно быть)
            List<string> target = comparator.PrepareComparison(properties); // испытуемый на соответствие эталону

            comparator.Compare(target, source, out List<string> delete_list, out List<string> insert_list);

            if (delete_list.Count == 0 && insert_list.Count == 0)
            {
                return; // success - проверка прошла успешно
            }

            FileLogger.Default.Write($"[{tableName}] {entityName}");

            if (delete_list.Count > 0)
            {
                FileLogger.Default.Write($"* delete (лишние поля)");

                foreach (string field in delete_list)
                {
                    FileLogger.Default.Write($"  - {field}");
                }
            }

            if (insert_list.Count > 0)
            {
                FileLogger.Default.Write($"* insert (отсутствующие поля)");

                foreach (string field in insert_list)
                {
                    FileLogger.Default.Write($"  - {field}");
                }
            }
        }
    }
}