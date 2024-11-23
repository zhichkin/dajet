## DaJet Script

[Реляционные базы данных](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/README.md)

### Мониторинг состояния таблиц-очередей

Ниже следующий скрипт реализует сервис DaJet Script (бесконечный цикл), который запрашивает состояние таблицы-очереди каждые 10 секунд. Результат выполнения скрипта сериализуется в ```JSON``` и отправляется в http-сервис мониторинга на стороне 1С.

```SQL
DECLARE @monitor  array  -- Данные мониторинга
DECLARE @response object -- HTTP ответ на запрос

WHILE TRUE -- Бесконечный цикл

   SLEEP 10 -- Периодичность выполнения мониторинга (секунды)

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

   REQUEST 'http://localhost/1c/hs/test/query'
      WHEN @monitor <> NULL
      WITH User-Agent   = 'DaJet Script'
         , Content-Type = 'application/json; charset=utf-8'
    SELECT Method  = 'POST'         -- HTTP метод запроса
         , OnError = 'continue'     -- break или continue
         , Content = JSON(@monitor) -- Тело HTTP запроса
      INTO @response -- { "Code": "200", "Value": "body" }

   IF @monitor = NULL
      THEN PRINT '[РегистрСведений.ИсходящиеСообщения] пусто'
      ELSE PRINT '[' + @response.Code + '] ' + @response.Value
   END

END -- WHILE
```

**Код обработчика http-запроса на стороне 1С**

В данном случае просто возвращается тело запроса клиента - скрипта DaJet Script.

![Код обработчика http-запроса на стороне 1С](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-database-queue-monitor-http-service.png)

**Результат выполнения запроса**
```
[2024-11-23 12:16:54] [200] [{"MsgType":"Справочник.Номенклатура","MsgCount":3,"DataSize":132,"AvgSize":44}]
[2024-11-23 12:17:04] [200] [{"MsgType":"Справочник.Номенклатура","MsgCount":3,"DataSize":132,"AvgSize":44}]
[2024-11-23 12:17:14] [200] [{"MsgType":"Справочник.Номенклатура","MsgCount":2,"DataSize":88,"AvgSize":44}]
[2024-11-23 12:17:24] [200] [{"MsgType":"Справочник.Номенклатура","MsgCount":2,"DataSize":88,"AvgSize":44}]
[2024-11-23 12:17:34] [200] [{"MsgType":"Справочник.Номенклатура","MsgCount":1,"DataSize":44,"AvgSize":44}]
[2024-11-23 12:17:44] [РегистрСведений.ИсходящиеСообщения] пусто
[2024-11-23 12:17:54] [РегистрСведений.ИсходящиеСообщения] пусто
[2024-11-23 12:18:04] [РегистрСведений.ИсходящиеСообщения] пусто
```

[Наверх](#мониторинг-состояния-таблиц-очередей)
