using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Scripting;
using System.Data;

namespace DaJet.Stream
{
    internal sealed class Processor : ProcessorBase
    {
        internal Processor(in Pipeline pipeline, in SqlStatement statement) : base(pipeline, statement) { }
        public override void Synchronize() { _next?.Synchronize(); }
        public override void Process()
        {
            if (_mode == StatementType.Processor)
            {
                ExecuteNonQuery(); //TODO: analyze {SELECT|INSERT|UPDATE|DELETE} ?
            }
            else if (_mode == StatementType.Buffering)
            {
                SetArrayVariable();
            }
            else if (_mode == StatementType.Streaming)
            {
                SetObjectVariable();
            }
            
            _next?.Process();
        }
        private void ExecuteNonQuery()
        {
            using (OneDbConnection connection = new(_pipeline.Context))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = _statement.Script;

                    ConfigureParameters(in command);

                    int result = command.ExecuteNonMagic();
                }
            }
        }
        private void SetArrayVariable()
        {
            List<DataObject> table = new();

            using (OneDbConnection connection = new(_pipeline.Context))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = _statement.Script;

                    ConfigureParameters(in command);

                    foreach (IDataReader reader in command.ExecuteNoMagic())
                    {
                        DataObject record = new(_statement.Mapper.Properties.Count);

                        _statement.Mapper.Map(in reader, in record);

                        table.Add(record);
                    }
                }
            }

            if (_descriptor is not null)
            {
                if (_pipeline.Parameters[_descriptor.Target] is DataObject target)
                {
                    target.SetValue(_descriptor.Member, table); //FIXME: optimize DataObject capacity
                }
            }
            else
            {
                _pipeline.Parameters[_arrayName] = table;
            }
        }
        private void SetObjectVariable()
        {
            using (OneDbConnection connection = new(_pipeline.Context))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = _statement.Script;

                    ConfigureParameters(in command);

                    //NOTE: ConfigureParameters method can use _objectName parameter
                    // !!!  before overriding it's value by the code below

                    _pipeline.Parameters[_objectName] = null;

                    foreach (IDataReader reader in command.ExecuteNoMagic())
                    {
                        DataObject record = new(_statement.Mapper.Properties.Count);

                        _statement.Mapper.Map(in reader, in record);

                        _pipeline.Parameters[_objectName] = record;

                        break; // take the first one record
                    }
                }
            }
        }
    }
}