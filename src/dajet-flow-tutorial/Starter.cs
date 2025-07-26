using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Metadata;
using DaJet.Model;

namespace DaJet.Flow.Tutorial
{
    /// <summary>
    /// Блок <b>Starter</b> выполняет функцию получения данных объектов конфигурации по их ссылкам.
    /// <br/>Смотри использование метода <see cref="MetadataProviderExtensions.Select(IMetadataProvider, Entity)"/>.
    /// <br/>Список ссылок формируется запросом, адрес которого указывается в настройке <see cref="StarterOptions.Script"/>,
    /// например, так: tutorial-data-export. В сочетании с настройкой <see cref="StarterOptions.Source"/>, в которой
    /// указывается база данных источник, может быть сформирован следующий адрес скрипта запроса: ms-demo/tutorial-data-export.
    /// <br/>Примеры запросов могут выглядеть следующим образом:
    /// <br/><c>SELECT Ссылка FROM Справочник.Номенклатура</c>
    /// <br/>или (таблица регистрации изменений планов обмена)
    /// <br/><c>SELECT Ссылка FROM Справочник.Номенклатура.Изменения</c>
    /// <br/>или (пакет запросов разных типов объектов конфигурации)
    /// <br/><c>SELECT Ссылка FROM Справочник.Номенклатура</c>
    /// <br/><c>SELECT Ссылка FROM Документ.Поступление</c>
    /// </summary>
    public sealed class Starter : ISourceBlock, IOutputBlock<DataObject>
    {
        private IInputBlock<DataObject> _next;
        public void LinkTo(in IInputBlock<DataObject> next)
        {
            // Этот метод вызывается фабрикой конвейеров менеджера конвейеров
            // сервера DaJet при сборке конвейера по настройкам,
            // определяемым пользователем в интерфейсе DaJet Studio или программно

            _next = next; // Запоминаем ссылку на следующий блок конвейера
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

            // 1. Получаем настройки для зарегистрированной на сервере DaJet базы данных по её псевдониму
            InfoBaseRecord database = dajet.Select<InfoBaseRecord>(_options.Source)
                ?? throw new InvalidOperationException($"Source database not found: {_options.Source}");

            // 2. Формируем адрес (url) скрипта, сохранённого на сервере DaJet для соответствующей базы данных
            string scriptPath = database.Name + "/" + _options.Script;

            // 3. Получаем скрипт по его адресу из хранилища сервера DaJet (dajet.db)
            ScriptRecord script = dajet.Select<ScriptRecord>(scriptPath)
                ?? throw new InvalidOperationException($"Script not found: {scriptPath}");

            if (string.IsNullOrWhiteSpace(script.Script))
            {
                throw new InvalidOperationException($"Script is empty: {scriptPath}");
            }

            _script = script.Script; // Запоминаем текст скрипта для дальнейшего его выполнения

            // 4. Получаем ссылку на провайдера метаданных соответствующей конфигурации базы данных из сервиса IMetadataService
            if (!metadata.TryGetOrCreate(in database, out IMetadataProvider context, out string error))
            {
                throw new InvalidOperationException(error);
            }

            _context = context; // Сохраняем ссылку на провайдера метаданных локально в блоке Starter
        }
        public void Execute()
        {
            // Создаём подключение к базе данных провайдера метаданных
            using (OneDbConnection connection = new(_context))
            {
                // Открываем подключение к базе данных
                connection.Open();

                // Создаём объект команды для выполнения запросов к базе данных
                using (OneDbCommand command = connection.CreateCommand())
                {
                    // Настраиваем параметры выполнения команды
                    command.CommandText = _script;

                    // Если в запросе или запросах скрипта есть параметры,
                    // то добавляем их следующим образом:
                    //command.Parameters.Add("ИмяПараметра", "Значение");

                    // 1. Способ чтения результата запроса при помощи DLR
                    // Смотри описание класса DaJet.Data.DataObject.
                    foreach (dynamic record in command.StreamReader())
                    {
                        // 1. Получаем ссылку из запроса
                        // DLR в действии - обращение к свойству через точку,
                        // а не при помощи метода record.GetValue("Ссылка")
                        Entity reference = record.Ссылка;

                        // 2. Используем аналог Ссылка.ПолучитьОбъект()
                        DataObject entity = _context.Select(reference);

                        // 3. Отправляем полученный объект дальше по конвейеру
                        _next?.Process(in entity);

                        // 4. Считаем количество обработанных объектов
                        counter++;
                    }

                    // 2. Способ чтения результата запроса, в том числе пакетного,
                    // состоящего из нескольких команд SELECT, идущих одна за другой
                    foreach (DataObject record in command.StreamReader())
                    {
                        // 1. Получаем ссылку из запроса
                        Entity reference = (Entity)record.GetValue(0);

                        // 2. Используем аналог Ссылка.ПолучитьОбъект()
                        DataObject entity = _context.Select(reference);

                        // 3. Отправляем полученный объект дальше по конвейеру
                        _next?.Process(in entity);

                        // 4. Считаем количество обработанных объектов
                        counter++;
                    }

                    // 3. Обычный способ чтения результата запроса, в том числе пакетного,
                    // состоящего из нескольких команд SELECT, идущих одна за другой
                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        do
                        {
                            while (reader.Read()) // Читаем данные текущего запроса
                            {
                                // 1. Получаем ссылку из запроса
                                Entity reference = (Entity)reader.GetValue(0);

                                // 2. Используем аналог Ссылка.ПолучитьОбъект()
                                DataObject entity = _context.Select(reference);

                                // 3. Отправляем полученный объект дальше по конвейеру
                                _next?.Process(in entity);

                                // 4. Считаем количество обработанных объектов
                                counter++;
                            }
                        }
                        while (reader.NextResult()); // Переключаемся на следующий запрос из пакета (скрипта)

                        reader.Close();
                    }
                }
            }
        }
        public void Dispose()
        {
            // Метод вызывается менеджером конвейеров DaJet после завершения работы
            // метода Execute (смотри выше) данного конвейера (блока Starter)

            // Выводим текущее значение счётчика обработанных объектов в монитор DaJet Studio
            _pipeline.UpdateMonitorStatus($"Processed {counter}");
        }
    }
}