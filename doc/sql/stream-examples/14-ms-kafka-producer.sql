
-- https://docs.confluent.io/platform/current/clients/producer.html

DECLARE @message object
DECLARE @empty_uuid uuid = "00000000-0000-0000-0000-000000000000"

-- ***********************************************************************
-- * Источник сообщений SQL Server - регистр сведений "ИсходящаяОчередь" *
-- ***********************************************************************
USE "mssql://ZHICHKIN/dajet-exchange"

DECLARE @Отправитель string = SELECT Код
                                FROM ПланОбмена.ПланОбменаРИБ
                               WHERE Предопределённый <> @empty_uuid

CONSUME TOP 1000
        НомерСообщения, ТипСообщения, ТелоСообщения
   INTO @message
   FROM РегистрСведений.ИсходящиеСообщения
  ORDER BY НомерСообщения ASC

-- *****************************************************
-- * Приёмник сообщений Apache Kafka, топик test-topic *
-- *****************************************************

PRODUCE 'kafka'
   WITH Acks = 'all'
      , ClientId = 'dajet'
      , MaxInFlight = 1
      , BootstrapServers = '192.168.239.177:9092'
      , MessageTimeoutMs = 30000
      , EnableIdempotence = false
 SELECT Key   = @Отправитель           -- Ключ сообщения
      , Value = @message.ТелоСообщения -- Тело сообщения
      , Topic = @message.ТипСообщения  -- Топик Kafka