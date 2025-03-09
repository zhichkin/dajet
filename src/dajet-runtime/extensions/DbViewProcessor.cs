using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Metadata.Services;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Runtime
{
    public sealed class DbViewProcessor : UserDefinedProcessor
    {
        private string _command;
        private string _outputFile;
        private IDbViewGenerator _generator;
        private IMetadataProvider _provider;
        public DbViewProcessor(in ScriptScope scope) : base(scope) { }
        public override void Process()
        {
            Initialize();

            _command = GetCommandName();
            _outputFile = GetOutputFileFullPath();
            string metadataName = GetMetadataObjectName();
            _generator.Options.Schema = GetDatabaseSchemaName();

            MetadataObject @object = _provider.GetMetadataObject(metadataName);

            if (@object is not ApplicationObject metadata)
            {
                //SetReturnValue($"[UNSUPPORTED] {metadataName}");
                FileLogger.Default.Write($"[UNSUPPORTED] {metadataName}");
            }
            else if (_command == "SCRIPT")
            {
                ScriptView(in _generator, in metadata, in metadataName);
            }
            else if (_command == "CREATE")
            {
                CreateView(in _generator, in metadata, in metadataName);
            }
            else if (_command == "DELETE")
            {
                DeleteView(in _generator, in metadata, in metadataName);
            }
            else
            {
                throw new InvalidOperationException($"[{nameof(DbViewProcessor)}] unknown command name \"{_command}\"");
            }

            _next?.Process();
        }
        private void Initialize()
        {
            if (_provider is not null) { return; }

            if (!_scope.TryGetMetadataProvider(out _provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            DbViewGeneratorOptions settings = new()
            {
                DatabaseProvider = _provider.DatabaseProvider,
                ConnectionString = _provider.ConnectionString
            };

            _generator = DaJet.Metadata.Services.DbViewGenerator.Create(settings);
        }
        private string GetCommandName()
        {
            _ = _scope.TryGetValue(_statement.Variables[0].Identifier, out object value);

            if (value is not string command)
            {
                throw new ArgumentException($"[{nameof(DbViewProcessor)}] command name is missing");
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
        private string GetMetadataObjectName()
        {
            foreach (ColumnExpression option in _statement.Options)
            {
                if (option.Alias == "ObjectName")
                {
                    if (StreamFactory.TryEvaluate(in _scope, option.Expression, out object value)
                        && value is string objectName
                        && !string.IsNullOrWhiteSpace(objectName))
                    {
                        return objectName;
                    }
                }
            }

            throw new InvalidOperationException($"[{nameof(DbViewProcessor)}] option \"ObjectName\" is not defined");
        }
        private string GetOutputFileFullPath()
        {
            foreach (ColumnExpression option in _statement.Options)
            {
                if (option.Alias == "OutputFile")
                {
                    if (StreamFactory.TryEvaluate(in _scope, option.Expression, out object value)
                        && value is string outputFile
                        && !string.IsNullOrWhiteSpace(outputFile))
                    {
                        return outputFile;
                    }
                }
            }

            return string.Empty;
        }
        private void SetReturnValue(in object value)
        {
            if (_statement.Return is not null)
            {
                if (!_scope.TrySetValue(_statement.Return.Identifier, value))
                {
                    throw new InvalidOperationException($"Error setting return variable {_statement.Return.Identifier}");
                }
            }
        }

        private void ScriptView(in IDbViewGenerator generator, in ApplicationObject metadata, in string metadataName)
        {
            try
            {
                using (StreamWriter writer = new(_outputFile, true, Encoding.UTF8))
                {
                    if (!generator.TryScriptView(in metadata, in writer, out string error))
                    {
                        //SetReturnValue($"[ERROR] {error}");
                        FileLogger.Default.Write($"[ERROR] {error}");
                    }
                }
            }
            catch (Exception exception)
            {
                //SetReturnValue($"[ERROR] {ExceptionHelper.GetErrorMessage(exception)}");
                FileLogger.Default.Write($"[ERROR] {ExceptionHelper.GetErrorMessage(exception)}");
            }
        }
        private void CreateView(in IDbViewGenerator generator, in ApplicationObject metadata, in string metadataName)
        {
            if (!generator.TryCreateView(in metadata, out string error))
            {
                //SetReturnValue($"[ERROR] {error}");
                FileLogger.Default.Write($"[ERROR] {error}");
            }
        }
        private void DeleteView(in IDbViewGenerator generator, in ApplicationObject metadata, in string metadataName)
        {
            try
            {
                generator.DropView(in metadata); // SetReturnValue("OK");
            }
            catch (Exception error)
            {
                //SetReturnValue($"[ERROR] {ExceptionHelper.GetErrorMessage(error)}");
                FileLogger.Default.Write($"[ERROR] {ExceptionHelper.GetErrorMessage(error)}");
            }
        }
    }
}