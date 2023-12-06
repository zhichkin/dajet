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
                    command.CommandText =
                        $"DECLARE @Ссылка entity; " +  // "{40:08ec069d-a06b-a1b1-11ee-93c449ca47e3}"
                        $"DECLARE @Наименование string; " + // "Товар 333"
                        $"SELECT Ссылка FROM {_options.Metadata} WHERE Ссылка = @Ссылка " +
                        $"SELECT Ссылка FROM Справочник.Номенклатура WHERE Наименование = @Наименование";

                    Entity customerOrder = Entity.Parse("{40:08ec069d-a06b-a1b1-11ee-93c449ca47e3}");

                    command.Parameters.Add("Ссылка", customerOrder);
                    command.Parameters.Add("Наименование", "Товар 333");

                    foreach (dynamic record in command.StreamReader())
                    {
                        Entity entity = record.Ссылка;

                        DataObject order = _metadata.GetDataObject(entity);

                        _next?.Process(in order);
                    }

                    foreach (DataObject record in command.StreamReader())
                    {
                        Entity entity = (Entity)record.GetValue(0);
                        
                        DataObject order = _metadata.GetDataObject(entity);
                        
                        _next?.Process(in order);
                    }
                    
                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        do
                        {
                            while (reader.Read())
                            {
                                DataObject record = new(reader.FieldCount); // memory buffer

                                reader.Map(in record);

                                Entity entity = record.GetEntity(0);

                                DataObject order = _metadata.GetDataObject(entity);

                                _next?.Process(in order);
                            }
                        }
                        while (reader.NextResult());

                        reader.Close();
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