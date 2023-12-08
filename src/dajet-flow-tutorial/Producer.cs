using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Metadata;
using DaJet.Model;

namespace DaJet.Flow.Tutorial
{
    /// <summary>
    /// Блок <b>Producer</b> выполняет функцию записи нового сообщения во входящую очередь базы данных приёмника.
    /// <br/>Псевдоним базы данных указывается в настройке <see cref="ProducerOptions.Target"/>.
    /// <br/>Имя входящей очереди указывается в настройке <see cref="ProducerOptions.QueueName"/>.
    /// <br/><b>Например:</b> "РегистрСведений.ВходящиеСообщения"
    /// </summary>
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
            // Создаём объект записи регистра сведений (входящей очереди),
            // полное наименование которого указывается в настройке "QueueName"
            // Например, "РегистрСведений.ВходящиеСообщения"
            DataObject record = _context.Create(_options.QueueName);

            // Генерируем следующий номер сообщения на основании данных регистра сведений.
            // Предлагаемый способ получения следующего номера сообщения носит демонстрационный характер.
            // В производственной среде рекомендуется использовать какой-нибудь более надёжный метод,
            // учитывающий конкуренцию со стороны других писателей во входящую очередь сообщений базы данных

            decimal sequence = 1M; // M это литерал decimal, чтобы не выполнять неявного преобразования int в decimal

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

            // Заполняем объект записи регистра сведений (входящая очередь)
            // Если в целевом регистре сведений есть какие-то ещё измерения, ресурсы или реквизиты,
            // которые данным кодом не будут учтены, то провайдер данных DaJet вычислит их по метаданным
            // и заполнит их значениями по умолчанию перед тем, как отправить в базу данных
            record.SetValue("НомерСообщения", sequence);
            record.SetValue("ОтметкаВремени", DateTime.Now);
            record.SetValue("ТипСообщения", input.GetValue("ТипСообщения"));
            record.SetValue("ТелоСообщения", input.GetValue("ТелоСообщения"));

            // Записываем новое сообщение во входящую очередь базы данных
            _context.Insert(in record);
        }
        public void Synchronize()
        {
            // Ничего не делаем
        }
        public void Dispose()
        {
            // Ничего не делаем
        }
    }
}