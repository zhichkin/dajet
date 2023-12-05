using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Metadata;
using DaJet.Model;

namespace DaJet.Flow.Tutorial
{
    public sealed class Starter : ISourceBlock, IOutputBlock<DataObject>
    {
        private IInputBlock<DataObject> _next;
        public void LinkTo(in IInputBlock<DataObject> next)
        {
            _next = next;
        }
        private readonly IPipeline _pipeline;
        private readonly StarterOptions _options;
        private readonly IMetadataProvider _metadata;
        public Starter(StarterOptions options, IPipeline pipeline, IDataSource dajet, IMetadataService metadata)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));

            if (dajet is null) { throw new ArgumentNullException(nameof(dajet)); }
            if (metadata is null) { throw new ArgumentNullException(nameof(metadata)); }

            InfoBaseRecord database = dajet.Select<InfoBaseRecord>(_options.Source)
                ?? throw new InvalidOperationException($"Source database not found: {_options.Source}");
            
            if (!metadata.TryGetMetadataProvider(database.Identity.ToString(), out IMetadataProvider context, out string error))
            {
                throw new InvalidOperationException(error);
            }

            _metadata = context;
        }
        public void Execute()
        {
            using (OneDbConnection connection = new(_metadata))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT Ссылка FROM {_options.Metadata}";

                    foreach (DataObject record in command.Stream())
                    {
                        Entity entity = (Entity)record.GetValue(0);
                        
                        DataObject order = connection.GetDataObject(entity);
                        
                        _next?.Process(in order);
                    }

                    foreach (dynamic record in command.Stream())
                    {
                        DataObject order = connection.GetDataObject(record.Ссылка);

                        _next?.Process(in order);
                    }
                }
            }
        }
        public void Dispose()
        {
            _pipeline.UpdateMonitorStatus("Starter stopped.");
        }
    }
}