using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Metadata;
using DaJet.Model;

namespace DaJet.Flow.Tutorial
{
    public sealed class Producer : IInputBlock<DataObject>
    {
        private readonly ProducerOptions _options;
        private readonly IMetadataProvider _context;
        public Producer(ProducerOptions options, IDataSource dajet, IMetadataService metadata)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (dajet is null) { throw new ArgumentNullException(nameof(dajet)); }
            if (metadata is null) { throw new ArgumentNullException(nameof(metadata)); }

            InfoBaseRecord database = dajet.Select<InfoBaseRecord>(_options.Target)
                ?? throw new InvalidOperationException($"Target database not found: {_options.Target}");

            if (!metadata.TryGetMetadataProvider(database.Identity.ToString(), out IMetadataProvider context, out string error))
            {
                throw new InvalidOperationException(error);
            }

            _context = context;
        }
        public void Process(in DataObject input)
        {
            DataObject record = _context.Create(_options.QueueName);

            decimal sequence = 1M;

            using (OneDbConnection connection = new(_context))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT MAX(НомерСообщения) FROM {_options.QueueName}";

                    object value = command.ExecuteScalar();

                    if (value is not null && !DBNull.Value.Equals(value))
                    {
                        sequence += (decimal)value;
                    }
                }
            }

            record.SetValue("НомерСообщения", sequence);
            record.SetValue("ОтметкаВремени", DateTime.Now);
            record.SetValue("ТипСообщения", input.GetValue("ТипСообщения"));
            record.SetValue("ТелоСообщения", input.GetValue("ТелоСообщения"));

            _context.Insert(in record);
        }
        public void Synchronize()
        {
            
        }
        public void Dispose()
        {
            
        }
    }
}