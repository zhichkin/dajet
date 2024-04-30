
-- ****************************************************************************
-- * Приёмник сообщений RabbitMQ, виртуальный хост dajet, топик test-exchange *
-- ****************************************************************************

PRODUCE 'amqp://guest:guest@localhost:5672/dajet'
   WITH Exchange   = 'test-exchange'
      , RoutingKey = 'AAA'
      , CarbonCopy = 'BBB,CCC,DDD'
      , MessageId  = 'msg-no-1234'
 SELECT AppId = 'test sender' -- Отправитель
      , Type  = 'Тестовый тип сообщения' -- Тип сообщения
      , Body  = 'Тестовое сообщение' -- Тело сообщения
      , ContentType = 'text/plain'