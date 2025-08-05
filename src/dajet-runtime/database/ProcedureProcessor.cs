﻿using DaJet.Data;
using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using System.Data;
using System.Data.Common;

namespace DaJet.Runtime
{
    public sealed class ProcedureProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private Uri _uri;
        private readonly string _procedureName;
        private readonly RequestStatement _statement;
        private readonly IDbConnectionFactory _factory;
        private Func<string, object, DbParameter> AddWithValueDelegate;
        public ProcedureProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not RequestStatement statement)
            {
                throw new ArgumentException(nameof(RequestStatement));
            }

            StreamFactory.BindVariables(in _scope);

            _statement = statement;

            _uri = _scope.GetUri(_statement.Target);

            _factory = DbConnectionFactory.GetFactory(in _uri);

            _procedureName = DbUriHelper.GetProcedureName(in _uri);
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose() { _next?.Dispose(); }
        private bool WhenIsTrue()
        {
            if (_statement.When is null) { return true; }

            SyntaxNode expression = _statement.When;

            return StreamFactory.Evaluate(in _scope, in expression);
        }
        public void Process()
        {
            if (WhenIsTrue())
            {
                Execute();
            }

            _next?.Process();
        }
        private void Execute()
        {
            using (DbConnection connection = _factory.Create(in _uri))
            {
                connection.Open();

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = _procedureName;
                    command.CommandType = CommandType.StoredProcedure;

                    if (AddWithValueDelegate is null)
                    {
                        if (command is SqlCommand ms)
                        {
                            AddWithValueDelegate = ms.Parameters.AddWithValue;
                        }
                        else if (command is NpgsqlCommand pg)
                        {
                            AddWithValueDelegate = pg.Parameters.AddWithValue;
                        }
                        else if (command is SqliteCommand sql)
                        {
                            AddWithValueDelegate = sql.Parameters.AddWithValue;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unsupported database: {command}");
                        }
                    }

                    ConfigureParameterValues(in command);

                    int rows_affected = command.ExecuteNonQuery();
                }
            }

            foreach (DbParameter parameter in output)
            {
                object value = parameter.Value == DBNull.Value ? null : parameter.Value;

                _scope.TrySetValue($"@{parameter.ParameterName}", value);
            }
        }
        private List<DbParameter> output = new();
        private void ConfigureParameterValues(in DbCommand command)
        {
            command.Parameters.Clear();

            foreach (ColumnExpression item in _statement.Headers)
            {
                if (item.Expression is VariableReference input)
                {
                    if (_scope.TryGetValue(input.Identifier, out object value))
                    {
                        value ??= DBNull.Value;

                        string name = string.IsNullOrEmpty(item.Alias) ? input.Identifier[1..] : item.Alias;

                        AddWithValueDelegate(name, value);
                    }
                }
            }
            
            output.Clear();

            if (_statement.Response is VariableReference variable)
            {
                if (_scope.TryGetValue(variable.Identifier, out object value))
                {
                    string name = variable.Identifier.TrimStart('@');
                    DbParameter parameter = AddWithValueDelegate(name, value);
                    parameter.Direction = ParameterDirection.Output;
                    output.Add(parameter);
                }
            }
        }
    }
}