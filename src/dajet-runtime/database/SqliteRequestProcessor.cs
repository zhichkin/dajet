using Confluent.Kafka;
using DaJet.Data;
using DaJet.Scripting.Model;
using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace DaJet.Runtime
{
    public sealed class SqliteRequestProcessor : DbRequestProcessor
    {
        public SqliteRequestProcessor(in ScriptScope scope) : base(in scope) { }
        protected override IEntityMapper CreateDataMapper()
        {
            return new SqliteEntityMapper();
        }
        protected override void ConfigureParameters(in DbCommand command)
        {
            if (command is not SqliteCommand cmd)
            {
                throw new InvalidOperationException();
            }

            cmd.Parameters.Clear();

            foreach (ColumnExpression option in Statement.Options)
            {
                if (!StreamFactory.TryEvaluate(Scope, option.Expression, out object value))
                {
                    throw new InvalidOperationException();
                }

                string name = option.Alias;

                if (value is null)
                {
                    cmd.Parameters.AddWithValue(name, DBNull.Value);
                }
                else if (value is bool boolean)
                {
                    cmd.Parameters.AddWithValue(name, boolean ? 1 : 0);
                }
                else if (value is int integer)
                {
                    cmd.Parameters.AddWithValue(name, integer);
                }
                else if (value is long int64)
                {
                    cmd.Parameters.AddWithValue(name, int64);
                }
                else if (value is decimal number)
                {
                    cmd.Parameters.AddWithValue(name, number);
                }
                else if (value is DateTime dateTime)
                {
                    cmd.Parameters.AddWithValue(name, dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                else if (value is string text)
                {
                    cmd.Parameters.AddWithValue(name, text);
                }
                else if (value is Guid uuid)
                {
                    cmd.Parameters.AddWithValue(name, uuid.ToByteArray());
                }
                else // byte[]
                {
                    cmd.Parameters.AddWithValue(name, value);
                }
            }
        }
    }
}