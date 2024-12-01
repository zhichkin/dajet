## DaJet Script

[RabbitMQ](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/rabbitmq/README.md)

### Управление брокером RabbitMQ

Управление брокером RabbitMQ осуществляется при помощи соответствующего [плагина управления](https://www.rabbitmq.com/docs/management). После его несложной установки становятся доступны web-интерфейс и web api для создания виртуальных хостов, топиков, очередей и прочего. Плагин доступен по порту ```15672```.

**Скрипт создания виртуального хоста, топика и очередей**

Следующий скрипт сначала читает настройки подключения к web api брокера RabbitMQ из информационной базы 1С:Предприятие 8. Затем по узлам плана обмена создаёт типик исходящих сообщений и выполняет привязку к нему очередей, которые затем могут быть использованы узлами 1С для получения сообщений. Данный скрипт выполняет конфигурирование RabbitMQ согласно методике [РИБ 2.0](https://github.com/zhichkin/dajet/blob/main/doc/distributed-info-bases-2-0.pdf).

> [Документация RabbitMQ web api](https://rawcdn.githack.com/rabbitmq/rabbitmq-server/v3.8.19/deps/rabbitmq_management/priv/www/api/index.html)

```SQL
DECLARE @node object
DECLARE @nodes array
DECLARE @options object
DECLARE @response object

DECLARE @vhost string = 'dajet-vhost'
DECLARE @topic string = 'dajet-topic'

USE 'mssql://server/database'

DECLARE @empty_uuid uuid = '00000000-0000-0000-0000-000000000000'
DECLARE @ЭтотУзел string = SELECT Код
                             FROM ПланОбмена.ПланОбменаРИБ
                            WHERE Предопределённый <> @empty_uuid

SELECT АдресСервера, ЛогинПароль
  INTO @options
  FROM РегистрСведений.НастройкиОбмена
 WHERE КодБрокера = 'RabbitMQ'

REQUEST '{@options.АдресСервера}/api/vhosts/{@vhost}?columns=name&disable_stats=true'
   WITH Content-Type  = 'application/json;charset=utf-8'
      , Authorization = 'Basic ' + @options.ЛогинПароль
 SELECT Method  = 'GET'
      , OnError = 'continue'
   INTO @response

REQUEST '{@options.АдресСервера}/api/vhosts/{@vhost}'
   WHEN @response.Code = '404'
   WITH Content-Type  = 'application/json;charset=utf-8'
      , Authorization = 'Basic ' + @options.ЛогинПароль
 SELECT Method  = 'PUT'
      , OnError = 'break'
      , Content = '{ "description": "DaJet Stream virtual host" }'
   INTO @response

REQUEST '{@options.АдресСервера}/api/exchanges/{@vhost}/{@topic}?columns=name&disable_stats=true'
   WITH Content-Type  = 'application/json;charset=utf-8'
      , Authorization = 'Basic ' + @options.ЛогинПароль
 SELECT Method  = 'GET'
      , OnError = 'continue'
   INTO @response

REQUEST '{@options.АдресСервера}/api/exchanges/{@vhost}/{@topic}'
   WHEN @response.Code = '404'
   WITH Content-Type  = 'application/json;charset=utf-8'
      , Authorization = 'Basic ' + @options.ЛогинПароль
 SELECT Method  = 'PUT'
      , OnError = 'break'
      , Content = '{ "type": "topic", "durable": true, "internal": false, "auto_delete": false }'
   INTO @response

SELECT route = Код,
       queue = @ЭтотУзел + '-' + Код
  INTO @nodes
  FROM ПланОбмена.ПланОбменаРИБ
 WHERE Код LIKE 'РИБ-%'
   AND ПометкаУдаления = FALSE

FOR EACH @node IN @nodes

  REQUEST '{@options.АдресСервера}/api/queues/{@vhost}/{@node.queue}?columns=name&disable_stats=true'
     WITH Content-Type  = 'application/json;charset=utf-8'
        , Authorization = 'Basic ' + @options.ЛогинПароль
   SELECT Method  = 'GET'
        , OnError = 'continue'
     INTO @response

  REQUEST '{@options.АдресСервера}/api/queues/{@vhost}/{@node.queue}'
     WHEN @response.Code = '404'
     WITH Content-Type  = 'application/json;charset=utf-8'
        , Authorization = 'Basic ' + @options.ЛогинПароль
   SELECT Method  = 'PUT'
        , OnError = 'break'
        , Content = '{ "durable": true, "auto_delete": false }'
     INTO @response

  REQUEST '{@options.АдресСервера}/api/bindings/{@vhost}/e/{@topic}/q/{@node.queue}?columns=destination'
     WITH Content-Type  = 'application/json;charset=utf-8'
        , Authorization = 'Basic ' + @options.ЛогинПароль
   SELECT Method  = 'GET'
        , OnError = 'continue'
     INTO @response

  REQUEST '{@options.АдресСервера}/api/bindings/{@vhost}/e/{@topic}/q/{@node.queue}'
     WHEN @response.Code = '200' AND @response.Value[destination=@node.queue].destination = ''
     WITH Content-Type  = 'application/json;charset=utf-8'
        , Authorization = 'Basic ' + @options.ЛогинПароль
   SELECT Method  = 'POST'
        , OnError = 'break'
        , Content = '{ "routing_key": "' + @node.route + '" }'
     INTO @response

END -- FOR

END -- Контекст базы данных
```

[Наверх](#управление-брокером-rabbitmq)

**Скрипт удаления виртуального хоста и всего его содержимого**

Скрипт полезен для очистки того, что было создано предыдущим скриптом. Сочетание этих двух скриптов очень помогает в развёртывании тестовых контуров с последующим их удалением.

```SQL
DECLARE @options object
DECLARE @response object

DECLARE @vhost string = 'dajet-vhost'

USE 'mssql://server/database'

DECLARE @empty_uuid uuid = '00000000-0000-0000-0000-000000000000'
DECLARE @ЭтотУзел string = SELECT Код
                             FROM ПланОбмена.ПланОбменаРИБ
                            WHERE Предопределённый <> @empty_uuid

SELECT АдресСервера, ЛогинПароль
  INTO @options
  FROM РегистрСведений.НастройкиОбмена
 WHERE КодБрокера = 'RabbitMQ'

REQUEST '{@options.АдресСервера}/api/vhosts/{@vhost}?columns=name&disable_stats=true'
   WITH Content-Type  = 'application/json;charset=utf-8'
      , Authorization = 'Basic ' + @options.ЛогинПароль
 SELECT Method = 'GET'
   INTO @response

REQUEST '{@options.АдресСервера}/api/vhosts/{@vhost}'
   WHEN @response.Code = '200'
   WITH Content-Type  = 'application/json;charset=utf-8'
      , Authorization = 'Basic ' + @options.ЛогинПароль
 SELECT Method = 'DELETE'
   INTO @response

END -- Контекст базы данных
```

[Наверх](#управление-брокером-rabbitmq)
