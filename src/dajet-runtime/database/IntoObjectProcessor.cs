using DaJet.Data;
using System.Data;
using System.Data.Common;

namespace DaJet.Runtime
{
    public sealed class IntoObjectProcessor : OneDbProcessor
    {
        private readonly IProcessor _append;
        public IntoObjectProcessor(in ScriptScope scope) : base(in scope)
        {
            _append = StreamFactory.CreateAppendStream(in scope, in _into);
        }
        public override void Process()
        {
            DataObject record = null;

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
                        if (reader.Read()) // take the first record
                        {
                            record = new(_statement.Mapper.Properties.Count);

                            _statement.Mapper.Map(in reader, in record);
                        }
                        reader.Close();
                    }
                }
            }

            _ = _scope.TrySetValue(_into.Identifier, record);

            if (record is not null)
            {
                _append?.Process();
            }
            
            _next?.Process();
        }
    }
}