using System.Data;
using System.Data.Common;

namespace DaJet.Stream
{
    public sealed class NonQueryProcessor : OneDbProcessor
    {
        public NonQueryProcessor(in StreamScope scope) : base(in scope) { }
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

                    int rows_affected = command.ExecuteNonQuery();
                }
            }

            _next?.Process();
        }
    }
}