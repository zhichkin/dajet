using DaJet.Data;
using DaJet.DbViewGenerator;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System.Data;
using System.Text;

namespace DaJet.Metadata.Services
{
    public abstract class DbViewGenerator : IDbViewGenerator
    {
        public static IDbViewGenerator Create(DbViewGeneratorOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            DbViewGenerator generator;

            if (options.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                generator = new MsDbViewGenerator(options);
            }
            else if (options.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                generator = new PgDbViewGenerator(options);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider: [{options.DatabaseProvider}].");
            }

            if (string.IsNullOrWhiteSpace(options.Schema))
            {
                generator.Options.Schema = generator.DEFAULT_SCHEMA_NAME;
            }

            return generator;
        }

        protected readonly IQueryExecutor _executor;
        protected readonly DbViewGeneratorOptions _options;
        public DbViewGenerator(DbViewGeneratorOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            _executor = QueryExecutor.Create(_options.DatabaseProvider, _options.ConnectionString);
        }
        public DbViewGeneratorOptions Options { get { return _options; } }

        #region "ABSTRACT MEMBERS"
        protected abstract string DEFAULT_SCHEMA_NAME { get; }
        protected abstract string DROP_VIEW_SCRIPT { get; }
        protected abstract string SELECT_VIEWS_SCRIPT { get; }
        protected abstract string DROP_SCHEMA_SCRIPT { get; }
        protected abstract string SCHEMA_EXISTS_SCRIPT { get; }
        protected abstract string CREATE_SCHEMA_SCRIPT { get; }
        protected abstract string SELECT_SCHEMA_SCRIPT { get; }
        protected abstract string FormatViewName(string viewName);
        public abstract string GenerateViewScript(in ApplicationObject metadata, string viewName);
        public abstract string GenerateEnumViewScript(in Enumeration enumeration, string viewName);
        #endregion

        #region "INTERFACE IMPLEMENTATION"

        public List<string> SelectSchemas()
        {
            List<string> list = new();

            int SCHEMA_NAME = 0;

            foreach (IDataReader reader in _executor.ExecuteReader(SELECT_SCHEMA_SCRIPT, 10))
            {
                if (reader.IsDBNull(SCHEMA_NAME))
                {
                    continue;
                }

                list.Add(reader.GetString(SCHEMA_NAME));
            }

            return list;
        }
        public bool SchemaExists(string name)
        {
            string script = string.Format(SCHEMA_EXISTS_SCRIPT, name);

            return (_executor.ExecuteScalar<int>(in script, 10) == 1);
        }
        public void CreateSchema(string name)
        {
            string script = string.Format(CREATE_SCHEMA_SCRIPT, name);

            _executor.ExecuteNonQuery(in script, 10);
        }
        public void DropSchema(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            string script = string.Format(DROP_SCHEMA_SCRIPT, name);

            _executor.ExecuteNonQuery(in script, 10);
        }

        public List<string> SelectViews(string? schema = null)
        {
            List<string> list = new();

            string select;
            int VIEW_NAME = 1;

            if (string.IsNullOrWhiteSpace(schema))
            {
                select = string.Format(SELECT_VIEWS_SCRIPT, _options.Schema);
            }
            else
            {
                select = string.Format(SELECT_VIEWS_SCRIPT, schema);
            }

            foreach (IDataReader reader in _executor.ExecuteReader(select, 20))
            {
                if (reader.IsDBNull(VIEW_NAME))
                {
                    continue;
                }

                list.Add(reader.GetString(VIEW_NAME));
            }

            return list;
        }

        public bool TryCreateView(in ApplicationObject metadata, out string error)
        {
            error = string.Empty;

            List<string> scripts = new();
            StringBuilder script = new();

            try
            {
                string viewName = Configurator.CreateViewName(metadata, _options.Codify);

                scripts.Add(string.Format(DROP_VIEW_SCRIPT, FormatViewName(viewName)));

                if (_options.Codify)
                {
                    script.AppendLine($"--{{{Configurator.CreateViewName(metadata)}}}");
                }

                if (metadata is Enumeration enumeration)
                {
                    script.AppendLine(GenerateEnumViewScript(enumeration, viewName));
                    scripts.Add(script.ToString());
                }
                else
                {
                    script.AppendLine(GenerateViewScript(metadata, viewName));
                    scripts.Add(script.ToString());

                    if (metadata is ITablePartOwner owner)
                    {
                        foreach (TablePart table in owner.TableParts)
                        {
                            viewName = Configurator.CreateViewName(metadata, table, _options.Codify);

                            scripts.Add(string.Format(DROP_VIEW_SCRIPT, FormatViewName(viewName)));

                            script.Clear();
                            if (_options.Codify)
                            {
                                script.AppendLine($"--{{{Configurator.CreateViewName(metadata, table)}}}");
                            }
                            script.AppendLine(GenerateViewScript(table, viewName));

                            scripts.Add(script.ToString());
                        }
                    }
                }

                _executor.TxExecuteNonQuery(scripts, 10);
            }
            catch (Exception exception)
            {
                error = $"[{metadata.Name}] [{metadata.TableName}] {ExceptionHelper.GetErrorMessage(exception)}";
            }

            return string.IsNullOrEmpty(error);
        }
        public bool TryCreateViews(in OneDbMetadataProvider cache, out int result, out List<string> errors)
        {
            result = 0;
            errors = new();
            
            foreach (string typeName in _options.MetadataTypes)
            {
                Guid type = MetadataTypes.ResolveName(typeName);

                if (type == Guid.Empty)
                {
                    errors.Add($"Metadata type [{typeName}] is not supported.");
                    continue;
                }

                foreach (MetadataItem item in cache.GetMetadataItems(type))
                {
                    MetadataObject metadata = cache.GetMetadataObject(item);

                    if (metadata is not ApplicationObject @object)
                    {
                        continue;
                    }

                    if (TryCreateView(in @object, out string error))
                    {
                        result++;
                    }
                    else
                    {
                        errors.Add(error);
                    }

                    if (!TryCreateChangeTrackingTableView(in cache, in @object, out string errorMessage))
                    {
                        errors.Add(error);
                    }
                }
            }

            return (errors.Count == 0);
        }
        private bool TryCreateChangeTrackingTableView(in OneDbMetadataProvider cache, in ApplicationObject entity, out string error)
        {
            error = string.Empty;
            List<string> scripts = new();
            StringBuilder script = new();
            ChangeTrackingTable table = null!;

            try
            {
                table = cache.GetChangeTrackingTable(entity);
            }
            catch (Exception exception)
            {
                error = $"[{entity.Name}] [ChangeTrackingTable] {ExceptionHelper.GetErrorMessage(exception)}";
            }

            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            if (table is null) { return true; }

            try
            {
                string viewName = Configurator.CreateViewName(entity, _options.Codify) + ".Изменения";

                scripts.Add(string.Format(DROP_VIEW_SCRIPT, FormatViewName(viewName)));

                if (_options.Codify)
                {
                    script.AppendLine($"--{{{Configurator.CreateViewName(entity)}}}");
                }

                script.AppendLine(GenerateViewScript(table, viewName));
                
                scripts.Add(script.ToString());

                _executor.TxExecuteNonQuery(scripts, 10);
            }
            catch (Exception exception)
            {
                error = $"[{table.Name}] [{table.TableName}] {ExceptionHelper.GetErrorMessage(exception)}";
            }

            return string.IsNullOrEmpty(error);
        }

        public int DropViews()
        {
            int result = 0;
            int VIEW_NAME = 1;

            string select = string.Format(SELECT_VIEWS_SCRIPT, _options.Schema);

            foreach (IDataReader reader in _executor.ExecuteReader(select, 30))
            {
                if (reader.IsDBNull(VIEW_NAME))
                {
                    continue;
                }

                string viewName = FormatViewName(reader.GetString(VIEW_NAME));

                string script = string.Format(DROP_VIEW_SCRIPT, viewName);

                _executor.ExecuteNonQuery(in script, 10);

                result++;
            }

            return result;
        }
        public void DropView(in ApplicationObject metadata)
        {
            List<string> scripts = new();

            string viewName = Configurator.CreateViewName(metadata, _options.Codify);

            scripts.Add(string.Format(DROP_VIEW_SCRIPT, FormatViewName(viewName)));

            if (metadata is ITablePartOwner owner)
            {
                foreach (TablePart table in owner.TableParts)
                {
                    viewName = Configurator.CreateViewName(metadata, table, _options.Codify);
                    scripts.Add(string.Format(DROP_VIEW_SCRIPT, FormatViewName(viewName)));
                }
            }

            _executor.TxExecuteNonQuery(scripts, 60);
        }

        public bool TryScriptViews(in OneDbMetadataProvider cache, in StreamWriter writer, out string error)
        {
            error = string.Empty;

            try
            {
                foreach (string typeName in _options.MetadataTypes)
                {
                    Guid type = MetadataTypes.ResolveName(typeName);

                    if (type == Guid.Empty)
                    {
                        writer.WriteLine($"-- Metadata type [{typeName}] is not supported.");
                        continue;
                    }

                    foreach (MetadataItem item in cache.GetMetadataItems(type))
                    {
                        MetadataObject metadata = cache.GetMetadataObject(item);

                        if (metadata is not ApplicationObject @object)
                        {
                            continue;
                        }

                        if (!TryScriptView(in @object, in writer, out string errorMessage))
                        {
                            writer.WriteLine($"/* {errorMessage} */");
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return string.IsNullOrWhiteSpace(error);
        }
        public bool TryScriptView(in ApplicationObject metadata, in StreamWriter writer, out string error)
        {
            error = string.Empty;

            try
            {
                string viewName = Configurator.CreateViewName(in metadata, _options.Codify);

                writer.WriteLine(string.Format(DROP_VIEW_SCRIPT, FormatViewName(viewName)));
                if (_options.DatabaseProvider == DatabaseProvider.SqlServer)
                {
                    writer.WriteLine("GO");
                }

                if (_options.Codify)
                {
                    writer.WriteLine($"--{{{Configurator.CreateViewName(metadata)}}}");
                }

                if (metadata is Enumeration enumeration)
                {
                    writer.WriteLine(GenerateEnumViewScript(enumeration, viewName));
                    if (_options.DatabaseProvider == DatabaseProvider.SqlServer)
                    {
                        writer.WriteLine("GO");
                    }
                }
                else
                {
                    writer.WriteLine(GenerateViewScript(metadata, viewName));
                    if (_options.DatabaseProvider == DatabaseProvider.SqlServer)
                    {
                        writer.WriteLine("GO");
                    }

                    if (metadata is ITablePartOwner owner)
                    {
                        foreach (TablePart table in owner.TableParts)
                        {
                            viewName = Configurator.CreateViewName(metadata, table, _options.Codify);

                            writer.WriteLine(string.Format(DROP_VIEW_SCRIPT, FormatViewName(viewName)));
                            if (_options.DatabaseProvider == DatabaseProvider.SqlServer)
                            {
                                writer.WriteLine("GO");
                            }

                            if (_options.Codify)
                            {
                                writer.WriteLine($"--{{{Configurator.CreateViewName(metadata, table)}}}");
                            }

                            writer.WriteLine(GenerateViewScript(table, viewName));
                            if (_options.DatabaseProvider == DatabaseProvider.SqlServer)
                            {
                                writer.WriteLine("GO");
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                error = $"[{metadata.Name}] [{metadata.TableName}] {ExceptionHelper.GetErrorMessage(exception)}";
            }

            return string.IsNullOrEmpty(error);
        }

        #endregion
    }
}