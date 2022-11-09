using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Data;
using System.Data.Common;

namespace DaJet.Data.Provider
{
    public sealed class OneDbCommand : DbCommand
    {
        private MetadataCache _metadata;

        private DbCommand? _command;
        private DbConnection? _connection;
        private DbTransaction? _transaction;
        private GeneratorResult _generator;
        public OneDbCommand(MetadataCache metadata)
        {
            _metadata = metadata;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _command?.Dispose();
            }

            _metadata = null;
        }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.Both;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override int CommandTimeout { get; set; } = 30;
        public override string CommandText { get; set; }
        protected override DbConnection? DbConnection
        {
            get { return _connection; }
            set { _connection = value; }
        }
        protected override DbTransaction? DbTransaction
        {
            get { return _transaction; }
            set { _transaction = value; }
        }
        protected override DbParameterCollection DbParameterCollection { get; } //TODO: !!!
        protected override DbParameter CreateDbParameter()
        {
            throw new NotImplementedException();
        }
        public override void Cancel()
        {
            _command?.Cancel();
        }
        public override void Prepare()
        {
            string error;
            ScriptModel model;

            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(CommandText, out model, out error))
                {
                    throw new Exception(error);
                }
            }

            //TODO: ConfigureParameters(in model);

            ScopeBuilder builder = new();

            if (!builder.TryBuild(in model, out ScriptScope scope, out error))
            {
                throw new Exception(error);
            }

            MetadataBinder binder = new();

            if (!binder.TryBind(in scope, in _metadata, out error))
            {
                throw new Exception(error);
            }

            ScriptTransformer transformer = new();

            if (!transformer.TryTransform(model, out error))
            {
                throw new Exception(error);
            }

            ISqlGenerator generator;

            if (_metadata.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                generator = new MsSqlGenerator() { YearOffset = _metadata.InfoBase.YearOffset };
            }
            else if (_metadata.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                generator = new PgSqlGenerator() { YearOffset = _metadata.InfoBase.YearOffset };
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider: {_metadata.DatabaseProvider}");
            }

            if (!generator.TryGenerate(in model, out _generator))
            {
                throw new Exception(_generator.Error);
            }
        }
        public override object? ExecuteScalar()
        {
            Prepare();

            using (_command = Connection.CreateCommand())
            {
                _command.CommandText = _generator.Script;

                using (DbDataReader reader = _command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return _generator.Mapper.Properties[0].GetValue(reader);
                    }
                }
            }

            return null!;
            
            //foreach (IDataReader reader in executor.ExecuteReader(result.Script, 10, Parameters))
            //{
            //    yield return result.Mapper.Map<TEntity>(in reader);
            //}
        }
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            return _command.ExecuteReader(behavior);
        }
        public override int ExecuteNonQuery()
        {
            throw new NotImplementedException();
        }
    }
}