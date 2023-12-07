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
        private int counter;
        private readonly string _script;
        private readonly IPipeline _pipeline;
        private readonly StarterOptions _options;
        private readonly IMetadataProvider _context;
        public Starter(StarterOptions options, IPipeline pipeline, IDataSource dajet, IMetadataService metadata)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));

            if (dajet is null) { throw new ArgumentNullException(nameof(dajet)); }
            if (metadata is null) { throw new ArgumentNullException(nameof(metadata)); }

            InfoBaseRecord database = dajet.Select<InfoBaseRecord>(_options.Source)
                ?? throw new InvalidOperationException($"Source database not found: {_options.Source}");

            string scriptPath = database.Name + "/" + _options.Script;

            ScriptRecord script = dajet.Select<ScriptRecord>(scriptPath)
                ?? throw new InvalidOperationException($"Script not found: {scriptPath}");

            if (string.IsNullOrWhiteSpace(script.Script))
            {
                throw new InvalidOperationException($"Script is empty: {scriptPath}");
            }

            _script = script.Script;

            if (!metadata.TryGetMetadataProvider(database.Identity.ToString(), out IMetadataProvider context, out string error))
            {
                throw new InvalidOperationException(error);
            }

            _context = context;
        }
        public void Execute()
        {
            using (OneDbConnection connection = new(_context))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = _script;

                    //command.Parameters.Add("ИмяПараметра", "Значение");

                    foreach (dynamic record in command.StreamReader())
                    {
                        Entity reference = record.Ссылка;

                        DataObject entity = _context.Select(reference);

                        _next?.Process(in entity); counter++;
                    }

                    foreach (DataObject record in command.StreamReader())
                    {
                        Entity reference = (Entity)record.GetValue(0);
                        
                        DataObject entity = _context.Select(reference);
                        
                        _next?.Process(in entity); counter++;
                    }
                    
                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        do
                        {
                            while (reader.Read())
                            {
                                Entity reference = (Entity)reader.GetValue(0);

                                DataObject entity = _context.Select(reference);

                                _next?.Process(in entity); counter++;
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
            _pipeline.UpdateMonitorStatus($"Processed {counter}");
        }
    }
}