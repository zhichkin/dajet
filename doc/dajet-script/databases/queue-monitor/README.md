## DaJet Script

[Реляционные базы данных](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/README.md)

### Мониторинг состояния таблиц-очередей

Ниже следующий скрипт реализует запрос состояния таблицы-очереди. Результат выполнения скрипта сериализуется в ```JSON``` и отправляется в http-сервис мониторинга.

```SQL
DECLARE @monitor  array  -- Данные мониторинга
DECLARE @response object -- HTTP ответ на запрос

USE 'mssql://server/database'

   SELECT MsgType  = ТипСообщения
        , MsgCount = COUNT(*)
        , DataSize = SUM(DATALENGTH(ТелоСообщения))
        , AvgSize  = AVG(DATALENGTH(ТелоСообщения))
     INTO @monitor
     FROM РегистрСведений.ИсходящиеСообщения
    GROUP BY ТипСообщения
    ORDER BY ТипСообщения ASC

END

IF @monitor = NULL
THEN PRINT 'Мониторинг [РегистрСведений.ИсходящиеСообщения] очередь пуста'
END

REQUEST 'http://localhost/1c/hs/monitor/out-queue'
   WHEN @monitor <> NULL
   WITH User-Agent   = 'DaJet Script'
      , Content-Type = 'application/json; charset=utf-8'
 SELECT Method  = 'POST'         -- HTTP метод запроса
      , OnError = 'continue'     -- break или continue
      , Content = JSON(@monitor) -- Тело HTTP запроса
   INTO @response -- { "Code": "200", "Value": "body" }

PRINT '[' + @response.Code + '] ' + @response.Value
```

**Результат выполнения запроса**
```
[2024-11-23 12:18:57] [200] [{"MsgType":"type 1","MsgCount":3,"DataSize":24,"AvgSize":8}]
```

**Код обработчика http-запроса на стороне 1С**
![Код обработчика http-запроса на стороне 1С](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-database-queue-monitor-http-service.png)

[Наверх](#мониторинг-состояния-таблиц-очередей)
