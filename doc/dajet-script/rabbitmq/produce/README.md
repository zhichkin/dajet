## DaJet Script

[RabbitMQ](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/rabbitmq/README.md)

### Команда PRODUCE

- [Регистр сведений исходящих сообщений](#регистр-сведений-исходящих-сообщений)
- [Базовый пример публикации сообщений](#базовый-пример-публикации-сообщений)
- [Пользовательские заголовки сообщений](#пользовательские-заголовки-сообщений)
- [Формирование пользовательских заголовков в коде DaJet Script](#формирование-пользовательских-заголовков-в-коде-dajet-script)

```SQL
PRODUCE 'amqp://<username>:<password>@<server>:<port>/<virtual-host>'
 SELECT <options>
```
**\<username\>** - имя пользователя<br>
**\<password\>** - пароль пользователя<br>
**\<server\>** - адрес сервера RabbitMQ<br>
**\<port\>** - порт сервера RabbitMQ<br>
**\<virtual-host\>** - виртуальный хост сервера RabbitMQ<br>
**\<options\>** - свойства сообщения RabbitMQ (смотри ниже)

> Строка подключения к брокеру RabbitMQ указывается в формате URL, следовательно, все специфические символы, например в пароле пользователя, должны быть указаны в URL-кодировке.

**Таблица свойств публикуемого сообщения RabbitMQ**

|**Свойство**|**Тип данных**|**Описание**|
|---|---|---|
|AppId|string|Наименование отправителя|
|Exchange|string|Наименование топика для публикации сообщения|
|RoutingKey|string|Ключ маршрутизации, если свойство Exchange заполнено.<br>В противном случае - наименование очереди для прямой отправки без маршрутизации.|
|Mandatory|number|Признак обязательности наличия очереди назначения. Подробнее смотри документацию RabbitMQ.<br>По умолчанию DaJet Script не использует этот флаг.|
|MessageId|string|Идентификатор сообщения|
|Type|string|Тип сообщения|
|Body|string|Тело сообщения: DaJet Script ориентируется на текстовые сообщения в кодировке **UTF-8**.|
|BlindCopy|string<br>(csv)|Дополнительные ключи маршрутизации, разделённые запятой. Значения ключей не доставляются получателю. [Документация RabbitMQ](https://www.rabbitmq.com/docs/sender-selected)|
|CarbonCopy|string<br>(csv)|Дополнительные ключи маршрутизации, разделённые запятой. Значения ключей доставляются получателю. Использовать не рекомендуется (оптимизация трафика).|
|ContentType|string|Тип содержимого тела сообщения.<br>Значение по умолчанию: **application/json**|
|ContentEncoding|string|Формат (кодировка) тела сообщения.<br>Значение по умолчанию: **UTF-8**|
|DeliveryMode|number|Вид доставки: 1 - in-memory, 2 - persistent.<br>Значение по умолчанию: 2 (сохранение на диск)|
|Priority|number|Приоритет доставки сообщения от 0 до 9<br>Значение по умолчанию: 0 (ноль)|
|ReplyTo|string|Адресат для обратной связи, определяемый логикой приложения.|
|CorrelationId|string|Идентификатор корреляции сообщений между собой, определяемый логикой приложения.|
|Expiration|string|Спецификация устаревания сообщения. Подробнее смотри документацию RabbitMQ. По умолчанию не используется.|
|Headers|object|Пользовательские заголовки (смотри документацию ниже)|

[Наверх](#команда-produce)

#### Регистр сведений исходящих сообщений

|**Реквизит**|**Назначение**|**Тип данных**|**Описание**|
|------------|--------------|--------------|------------|
|НомерСообщения|Измерение|ЧИСЛО(15,0)|Номер сообщения|
|Заголовки|Ресурс|СТРОКА(1024)|Заголовки сообщения|
|ТипСообщения|Ресурс|СТРОКА(1024)|Тип сообщения|
|ТелоСообщения|Ресурс|СТРОКА(0)|Тело сообщения|
|Получатель|Реквизит|СТРОКА(36)|Код получателя|

![outgoing-queue-data](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-rabbitmq-produce-outgoing-queue.png)

[Наверх](#команда-produce)

#### Базовый пример публикации сообщений

```SQL
DECLARE @message object

USE 'mssql://server/database'

   CONSUME TOP 1000
           Получатель
         , НомерСообщения
         , ТипСообщения
         , ТелоСообщения
      INTO @message
      FROM РегистрСведений.ИсходящиеСообщения
     ORDER BY НомерСообщения ASC
    
   PRODUCE 'amqp://guest:guest@localhost:5672/dajet'
    SELECT AppId      = 'Центральный офис'
         , Exchange   = 'test-exchange'
         , RoutingKey = @message.Получатель
         , MessageId  = @message.НомерСообщения
         , Type       = @message.ТипСообщения
         , Body       = @message.ТелоСообщения

END
```

![message-example](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-rabbitmq-produce-message-example.png)

[Наверх](#команда-produce)

#### Пользовательские заголовки сообщений

Пользовательские заголовки - это своего рода метаданные о сообщении. Их полезно использовать для указания дополнительных сведений, например, о версии формата JSON в теле сообщения. Заголовки используются для быстрого получения информации о том как обработать или маршрутизировать сообщение. Таким образом отпадает необходимость парсинга всего тела сообщения, которое может быть большим, и оптимизируется обмен данными.

Команда **PRODUCE** позволяет работать с пользовательскими заголовками сообщений RabbitMQ. В следующем примере заголовки хранятся в формате JSON в исходящей очереди регистра сведений. Чтобы их отправить в очередь RabbitMQ, необходимо эти заголовки десериализовать при помощи функции **JSON** в объект типа ```object```, а затем присвоить полученное значение свойству ```Headers``` сообщения RabbitMQ.

![outgoing-headers](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-rabbitmq-produce-outgoing-headers.png)

```SQL
DECLARE @message object

USE 'mssql://server/database'

   CONSUME TOP 1000
           Получатель
         , Заголовки
         , НомерСообщения
         , ТипСообщения
         , ТелоСообщения
      INTO @message
      FROM РегистрСведений.ИсходящиеСообщения
     ORDER BY НомерСообщения ASC
    
   PRODUCE 'amqp://guest:guest@localhost:5672/dajet'
    SELECT AppId      = 'Центральный офис'
         , Exchange   = 'test-exchange'
         , Headers    = JSON(@message.Заголовки)
         , RoutingKey = @message.Получатель
         , MessageId  = @message.НомерСообщения
         , Type       = @message.ТипСообщения
         , Body       = @message.ТелоСообщения

END
```

![message-headers](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-rabbitmq-produce-message-headers.png)

[Наверх](#команда-produce)

#### Формирование пользовательских заголовков в коде DaJet Script

```SQL
DECLARE @message object
DECLARE @headers object

USE 'mssql://server/database'

   CONSUME TOP 1000
           Получатель
         , НомерСообщения
         , ТипСообщения
         , ТелоСообщения
      INTO @message
      FROM РегистрСведений.ИсходящиеСообщения
     ORDER BY НомерСообщения ASC

   -- Формирование пользовательских заголовков
   SET @headers = SELECT version = '1.0'

   PRODUCE 'amqp://guest:guest@localhost:5672/dajet'
    SELECT AppId      = 'Центральный офис'
         , Exchange   = 'test-exchange'
         , Headers    = @headers -- Заголовки
         , RoutingKey = @message.Получатель
         , MessageId  = @message.НомерСообщения
         , Type       = @message.ТипСообщения
         , Body       = @message.ТелоСообщения

END
```

![message-headers-by-code](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-rabbitmq-produce-message-headers-by-code.png)

[Наверх](#команда-produce)
