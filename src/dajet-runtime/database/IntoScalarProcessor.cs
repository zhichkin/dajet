using DaJet.Data;
using System.Data;
using System.Data.Common;

namespace DaJet.Runtime
{
    public sealed class IntoScalarProcessor : OneDbProcessor
    {
        private readonly IProcessor _append;
        public IntoScalarProcessor(in ScriptScope scope) : base(in scope) { }
        public override void Process()
        {
            object value = null;

            using (DbConnection connection = _factory.Create(in _uri))
            {
                connection.Open();

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = _statement.Script;

                    InitializeParameterValues();

                    _factory.ConfigureParameters(in command, in _parameters, _yearOffset);

                    using (IDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read()) // take the first value of the first record
                        {
                            value = _statement.Mapper.Properties[0].GetValue(in reader);
                        }
                        reader.Close();
                    }
                }
            }

            _ = _scope.TrySetValue(_into.Identifier, value);
            
            _next?.Process();
        }
    }
}