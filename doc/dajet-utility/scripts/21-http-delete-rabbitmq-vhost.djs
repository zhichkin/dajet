-- https://rawcdn.githack.com/rabbitmq/rabbitmq-server/v3.8.19/deps/rabbitmq_management/priv/www/api/index.html

DECLARE @options object
DECLARE @response object

DECLARE @vhost string = 'dajet-vhost'

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
 SELECT Method = 'GET'
   INTO @response

REQUEST '{@options.АдресСервера}/api/vhosts/{@vhost}'
   WHEN @response.Code = '200'
   WITH Content-Type  = 'application/json;charset=utf-8'
      , Authorization = 'Basic ' + @options.ЛогинПароль
 SELECT Method = 'DELETE'
   INTO @response

END -- Контекст базы данных
