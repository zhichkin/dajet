using DaJet.Data;
using System.Data;
using System.Data.Common;

namespace DaJet.Runtime
{
    public sealed class IntoArrayProcessor : OneDbProcessor
    {
        public IntoArrayProcessor(in StreamScope scope) : base(in scope) { }
        public override void Process()
        {
            List<DataObject> table = new();

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
                        while (reader.Read())
                        {
                            DataObject record = new(_statement.Mapper.Properties.Count);

                            _statement.Mapper.Map(in reader, in record);

                            table.Add(record);
                        }
                        reader.Close();
                    }
                }
            }

            _ = _scope.TrySetValue(_into.Identifier, table);

            _next?.Process();
        }
    }
}