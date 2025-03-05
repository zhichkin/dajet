DECLARE @Получатель string
DECLARE @ТипСообщения string
DECLARE @ТелоСообщения string

PRODUCE 'amqp://guest:guest@localhost:5672/dajet'
 SELECT AppId      = 'Центральная база'
      , Exchange   = 'test-exchange'
      , RoutingKey = @Получатель
      , Type       = @ТипСообщения
      , Body       = @ТелоСообщения