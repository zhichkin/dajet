using DaJet.Data;
using DaJet.Scripting.Model;
using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace DaJet.Runtime
{
    public sealed class SqliteRequestProcessor : DbRequestProcessor
    {
        private readonly SqliteDataMapper _mapper = new();
        public SqliteRequestProcessor(in ScriptScope scope) : base(in scope) { }
        protected override IEntityMapper CreateDataMapper() { return _mapper; }
        protected override void ConfigureParameters(in DbCommand command)
        {
            if (command is not SqliteCommand cmd)
            {
                throw new InvalidOperationException();
            }

            cmd.Parameters.Clear();

            Dictionary<string, object> parameters = new(Statement.Options.Count);

            foreach (ColumnExpression option in Statement.Options)
            {
                if (!StreamFactory.TryEvaluate(Scope, option.Expression, out object value))
                {
                    throw new InvalidOperationException();
                }

                parameters.Add(option.Alias, value);
            }

            _mapper.Map(command, in parameters);
        }
    }
}