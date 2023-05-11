using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Data;
using System.Data.Common;

namespace DaJet.Data.Provider
{
    public sealed class OneDbCommand : DbCommand
    {
        private readonly OneDbParameterCollection _parameters = new();

        private IMetadataProvider _metadata;

        private DbCommand? _command;
        private DbConnection? _connection;
        private DbTransaction? _transaction;
        private GeneratorResult _generator;
        public OneDbCommand(IMetadataProvider metadata)
        {
            _metadata = metadata;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _command?.Dispose();
            }
            _metadata = null!;
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
        public new OneDbParameterCollection Parameters { get { return _parameters; } }
        protected override DbParameterCollection DbParameterCollection { get { return Parameters; } }
        protected override DbParameter CreateDbParameter() { return new OneDbParameter(); }
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

            ConfigureParameters(in model);

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

            if (!generator.TryGenerate(in model, in _metadata, out _generator))
            {
                throw new Exception(_generator.Error);
            }
        }
        private void ConfigureParameters(in ScriptModel model)
        {
            foreach (OneDbParameter parameter in Parameters)
            {
                if (parameter.Value is Guid uuid)
                {
                    parameter.Value = SQLHelper.GetSqlUuid(uuid);
                    parameter.Size = 16;
                    parameter.DbType = DbType.Binary;
                }
                else if (parameter.Value is bool boolean)
                {
                    if (_metadata.DatabaseProvider == DatabaseProvider.SqlServer)
                    {
                        parameter.Value = new byte[] { Convert.ToByte(boolean) };
                        parameter.Size = 1;
                        parameter.DbType = DbType.Binary;
                    }
                }
                else if (parameter.Value is Entity entity)
                {
                    parameter.Value = SQLHelper.GetSqlUuid(entity.Identity);
                    parameter.Size = 16;
                    parameter.DbType = DbType.Binary;
                }
                else if (parameter.Value is DateTime dateTime)
                {
                    parameter.Value = dateTime.AddYears(_metadata.InfoBase.YearOffset);
                    parameter.DbType = DbType.DateTime2;
                }

                if (DeclareStatementExists(in model, parameter.ParameterName))
                {
                    continue;
                }

                if (parameter.Value == null)
                {
                    continue; // TODO TokenType.NULL
                }

                Type parameterType = parameter.Value.GetType();

                DeclareStatement declare = new()
                {
                    Name = "@" + parameter.ParameterName,
                    Type = new TypeIdentifier() { Identifier = ScriptHelper.GetDataTypeLiteral(parameterType) },
                    Initializer = new ScalarExpression()
                    {
                        Token = ScriptHelper.GetDataTypeToken(parameterType),
                        Literal = parameter.Value.ToString()!
                    }
                };

                model.Statements.Insert(0, declare);
            }
        }
        private bool DeclareStatementExists(in ScriptModel model, string name)
        {
            foreach (SyntaxNode statement in model.Statements)
            {
                if (statement is not DeclareStatement declare)
                {
                    continue;
                }

                if (declare.Name.Substring(1) == name) // remove leading @ or &
                {
                    return true;
                }
            }
            return false;
        }
        public override object? ExecuteScalar()
        {
            Prepare();

            using (_command = Connection.CreateCommand())
            {
                _command.CommandText = _generator.Script;

                foreach (OneDbParameter parameter in Parameters)
                {
                    DbParameter p = _command.CreateParameter();
                    p.Value = parameter.Value;
                    p.ParameterName = parameter.ParameterName;
                    _ = _command.Parameters.Add(p);
                }

                using (DbDataReader reader = _command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return _generator.Mapper.Properties[0].GetValue(reader);
                    }
                }
            }

            return null!;
        }
        public new OneDbDataReader ExecuteReader()
        {
            return ExecuteDbDataReader(CommandBehavior.Default);
        }
        protected override OneDbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            Prepare();
            
            _command = Connection.CreateCommand();
            _command.CommandText = _generator.Script;
            
            foreach (OneDbParameter parameter in Parameters)
            {
                DbParameter p = _command.CreateParameter();
                p.Value = parameter.Value;
                p.ParameterName = parameter.ParameterName;
                _ = _command.Parameters.Add(p);
            }

            DbDataReader reader = _command.ExecuteReader(behavior);
            return new OneDbDataReader(in reader, in _generator);
        }
        public override int ExecuteNonQuery()
        {
            throw new NotImplementedException();
        }
    }
}