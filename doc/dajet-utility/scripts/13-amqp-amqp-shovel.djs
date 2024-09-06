DECLARE @source  string = 'guest:guest@localhost:5672/dajet'
DECLARE @target  string = 'guest:guest@localhost:5672/dajet'
DECLARE @message object

-- ***************************************************************************
-- * Источник сообщений RabbitMQ, виртуальный хост dajet, очередь test-queue *
-- ***************************************************************************

CONSUME 'amqp://{@source}'
   WITH QueueName = 'test-queue', Heartbeat = 5
   INTO @message

-- ****************************************************************************
-- * Приёмник сообщений RabbitMQ, виртуальный хост dajet, очередь test-shovel *
-- ****************************************************************************
PRODUCE 'amqp://{@target}'
 SELECT AppId = @message.AppId
      , Type  = @message.Type
      , Body  = @message.Body
      , RoutingKey = 'test-shovel'
      , MessageId  = @message.MessageId