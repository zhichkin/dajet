using DaJet.Data;
using System.Data;
using System.Data.Common;

namespace DaJet.Stream
{
    public sealed class IntoObjectProcessor : OneDbProcessor
    {
        public IntoObjectProcessor(in StreamScope scope) : base(in scope) { }
        public override void Process()
        {
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
                            DataObject record = new(_statement.Mapper.Properties.Count);

                            _statement.Mapper.Map(in reader, in record);

                            _ = _scope.TrySetValue(_into.Identifier, record);
                        }
                        reader.Close();
                    }
                }
            }

            _next?.Process();
        }
    }
}