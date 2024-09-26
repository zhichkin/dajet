using DaJet.Data;
using DaJet.Scripting.Model;
using System.Data;
using System.Data.Common;

namespace DaJet.Runtime
{
    public sealed class AppendObjectProcessor : OneDbProcessor
    {
        private readonly string _member;
        public AppendObjectProcessor(in StreamScope scope, in VariableReference target, in string member) : base(in scope)
        {
            _into = target ?? throw new ArgumentNullException(nameof(target));

            _member = !string.IsNullOrEmpty(member) ? member : throw new ArgumentNullException(nameof(member));
        }
        public override void Process()
        {
            DataObject record = new(_statement.Mapper.Properties.Count);

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
                            _statement.Mapper.Map(in reader, in record);
                        }
                        reader.Close();
                    }
                }
            }

            if (_scope.TryGetValue(_into.Identifier, out object target))
            {
                if (target is DataObject entity)
                {
                    entity.SetValue(_member, record);
                }
            }

            _next?.Process();
        }
    }
}