DECLARE @response object -- Переменная для ответа на HTTP запрос
DECLARE @object object -- Объект метаданных конфигурации 1С:Предприятие 8
DECLARE @Команда string = 'http-metadata-registry'
DECLARE @Счётчик number = 0

PRINT '[' + @Команда + '] НАЧАЛО'

USE 'mssql://zhichkin/unf'

   PROCESS @Команда WITH DaJet.Runtime.MetadataStreamer INTO @object

   REQUEST 'http://localhost/metadata-registry/hs/metadata/registry'
      WITH User-Agent   = 'DaJet Metadata Streamer'
         , Content-Type = 'text/plain; charset=utf-8'
    SELECT Method  = 'POST'        -- HTTP метод запроса
         , Content = JSON(@object) -- Тело HTTP запроса
      INTO @response -- { "Code": "200", "Value": "text" }

   IF @response.Code = '200' THEN
      SET @Счётчик = @Счётчик + 1
   ELSE
      PRINT '[' + @response.Code + '] ' + @response.Value
   END

END -- Конец выполнения потокового процессора MetadataStreamer

PRINT '[' + @Команда + '] Выполнено ' + @Счётчик + ' запросов'

RETURN '[' + @Команда + '] КОНЕЦ'