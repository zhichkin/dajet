
DECLARE @node object
DECLARE @nodes array
DECLARE @options object
DECLARE @response object

DECLARE @vhost string = 'dajet-vhost'
DECLARE @topic string = 'dajet-topic'

USE 'mssql://zhichkin/dajet-exchange'

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