
-- https://docs.confluent.io/platform/current/clients/consumer.html

DECLARE @Источник string = 'test-topic'
DECLARE @Приёмник string = 'pgsql://postgres:postgres@127.0.0.1:5432/dajet-exchange'
DECLARE @message object

-- *******************************************************
-- * Источник сообщений Apache Kafka, топик "test-topic" *
-- *******************************************************

CONSUME 'kafka'
   WITH Topic               = @Источник              -- Топик Apache Kafka
      , GroupId             = 'dajet'                -- group.id
      , ClientId            = 'dajet'                -- client.id
      , BootstrapServers    = '192.168.238.182:9092' -- bootstrap.servers (csv)
      , EnableAutoCommit    = false                  -- enable.auto.commit
      , AutoOffsetReset     = 'earliest'             -- auto.offset.reset
      , SessionTimeoutMs    = 60000                  -- session.timeout.ms
      , HeartbeatIntervalMs = 20000                  -- heartbeat.interval.ms
   INTO @message

-- ************************************************************
-- * Приёмник сообщений, регистр сведений "ВходящиеСообщения" *
-- ************************************************************
USE '{@Приёмник}'

INSERT РегистрСведений.ВходящиеСообщения
SELECT НомерСообщения = VECTOR('so_incoming_queue')
     , ОтметкаВремени = NOW()
     , Отправитель    = @message.Topic
     , ТипСообщения   = @message.Key
     , ТелоСообщения  = @message.Value