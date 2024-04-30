
-- https://docs.confluent.io/platform/current/clients/consumer.html

-- *****************************************************
-- * Приёмник сообщений Apache Kafka, топик test-topic *
-- *****************************************************

DECLARE @message object

CONSUME 'kafka'
   WITH Topic = 'test-topic'
      , GroupId = 'dajet'
      , ClientId = 'dajet'
      , BootstrapServers = '192.168.239.177:9092'
      , EnableAutoCommit = false
      , AutoOffsetReset  = 'earliest'
      , SessionTimeoutMs = 60000
      , HeartbeatIntervalMs = 20000
   INTO @message

-- ************************************************************************
-- * Приёмник сообщений SQL Server - регистр сведений "ВходящиеСообщения" *
-- ************************************************************************
USE 'mssql://ZHICHKIN/dajet-exchange'

INSERT РегистрСведений.ВходящиеСообщения
SELECT НомерСообщения = VECTOR('so_incoming_queue')
     , ОтметкаВремени = NOW()
     , Отправитель    = @message.Key
     , ТипСообщения   = @message.Topic
     , ТелоСообщения  = @message.Value