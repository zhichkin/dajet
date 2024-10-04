
-- https://docs.confluent.io/platform/current/clients/producer.html

DECLARE @Источник string = 'mssql://zhichkin/dajet-exchange'
DECLARE @Приёмник string = 'test-topic'
DECLARE @changes object
DECLARE @message object

-- ********************************************************************************
-- * Источник сообщений, таблица регистрации изменений справочника "Номенклатура" *
-- ********************************************************************************
USE '{@Источник}'

DECLARE @Получатель entity = SELECT Ссылка
                               FROM ПланОбмена.ПланОбменаРИБ
                              WHERE Код = 'KAFKA'
                                AND ПометкаУдаления = false

CONSUME TOP 1000 Ссылка INTO @changes
   FROM Справочник.Номенклатура.Изменения
  WHERE УзелОбмена = @Получатель
  ORDER BY Ссылка ASC

SELECT Ссылка          = UUIDOF(Ссылка)
     , Код             = LTRIM(RTRIM(Данные.Код))
     , Наименование    = Данные.Наименование
     , ПометкаУдаления = Данные.ПометкаУдаления
  INTO @message
  FROM Справочник.Номенклатура AS Данные
APPEND (SELECT Период, Цена
          FROM РегистрСведений.ЦеныНоменклатуры
         WHERE Номенклатура = @changes.Ссылка
         ORDER BY Период ASC) AS Цены
 WHERE Данные.Ссылка = @changes.Ссылка

-- *******************************************************
-- * Приёмник сообщений Apache Kafka, топик "test-topic" *
-- *******************************************************
PRODUCE 'kafka'
   WITH Acks              = 'all'                  -- acks
      , ClientId          = 'dajet'                -- client.id
      , MaxInFlight       = 1                      -- max.in.flight
      , BootstrapServers  = '192.168.238.182:9092' -- bootstrap.servers (csv)
      , MessageTimeoutMs  = 30000                  -- message.timeout.ms
      , EnableIdempotence = false                  -- enable.idempotence
 SELECT Key   = 'Справочник.Номенклатура' -- Ключ сообщения
      , Value = JSON(@message)      -- Тело сообщения
      , Topic = @Приёмник                 -- Топик Apache Kafka

END -- Контекст базы данных источника
