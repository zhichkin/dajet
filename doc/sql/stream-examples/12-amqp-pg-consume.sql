DECLARE @source  string = 'guest:guest@localhost:5672/dajet'
DECLARE @target  string = 'postgres:postgres@127.0.0.1:5432/dajet-exchange'
DECLARE @message object

-- ***************************************************************************
-- * Источник сообщений RabbitMQ, виртуальный хост dajet, очередь test-queue *
-- ***************************************************************************

CONSUME 'amqp://{@source}'
   WITH QueueName = 'test-queue', Heartbeat = 5
   INTO @message

-- ************************************************************************
-- * Приёмник сообщений SQL Server - регистр сведений "ВходящиеСообщения" *
-- ************************************************************************
USE 'pgsql://{@target}'

INSERT РегистрСведений.ВходящиеСообщения
SELECT НомерСообщения = VECTOR('so_incoming_queue')
     , ОтметкаВремени = NOW()
     , Отправитель    = @message.AppId
     , ТипСообщения   = @message.Type
     , ТелоСообщения  = @message.Body
     , Получатели     = @message.ContentType