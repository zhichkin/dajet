## DaJet Script

[RabbitMQ](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/rabbitmq/README.md)

### Команда CONSUME
- [Таблица свойств потребляемого сообщения RabbitMQ](#таблица-свойств-потребляемого-сообщения-rabbitmq)
- [Пример кода DaJet Script](#пример-кода-dajet-script)
- [Обработка пользовательских заголовков](#обработка-пользовательских-заголовков)

Поведение команды **CONSUME**, реализующей потребление сообщений RabbitMQ, с точки зрения концепции потоковой обработки данных DaJet Script аналогично поведению команды [CONSUME](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/databases/consume/README.md) для баз данных.

```SQL
CONSUME 'amqp://<username>:<password>@<server>:<port>/<virtual-host>'
   WITH <options>
   INTO <variable>
```
**\<username\>** - имя пользователя<br>
**\<password\>** - пароль пользователя<br>
**\<server\>** - адрес сервера RabbitMQ<br>
**\<port\>** - порт сервера RabbitMQ<br>
**\<virtual-host\>** - виртуальный хост сервера RabbitMQ<br>
**\<options\>** - параметры команды **CONSUME** (смотри ниже)

> Строка подключения к брокеру RabbitMQ указывается в формате URL, следовательно, все специфические символы, например в пароле пользователя, должны быть указаны в URL-кодировке.

**Таблица параметров команды CONSUME**

|**Свойство**|**Тип данных**|**Описание**|
|---|---|---|
|QueueName|string|Наименование очереди для получения сообщений|
|Heartbeat|number|Период проверки в секундах наличия подключения к RabbitMQ. В случае его потери - автоматическое восстановление. Кроме этого данное значение используется для периодического вывода количества обработанных (успешно полученных) сообщений в журнал утилиты **dajet**.|
|PrefetchSize|number|Размер клиентского буфера в байтах.<br>Значение по умолчанию: **неограниченно**.|
|PrefetchCount|number|Количество сообщений, которые могут быть отправлены сервером без подтверждения.<br>Значение по умолчанию: **1**. [Документация RabbitMQ](https://www.rabbitmq.com/docs/consumer-prefetch)<br>Значение 1 выбрано DaJet Script для обеспечения гарантий доставки **at-least-once-in-order** как наиболее простой и достаточный в большинстве случаев вариант, в том числе с точки зрения производительности.|

> **На заметку:** [хорошая статья](https://www.cloudamqp.com/blog/how-to-optimize-the-rabbitmq-prefetch-count.html), объясняющая основные принципы оптимизации потребления сообщений при помощи параметра **PrefetchCount**.

[Наверх](#команда-consume)

#### Таблица свойств потребляемого сообщения RabbitMQ

|**Свойство**|**Тип данных**|**Описание**|
|---|---|---|
|AppId|string|Наименование отправителя|
|MessageId|string|Идентификатор сообщения|
|Type|string|Тип сообщения|
|Body|string|Тело сообщения: DaJet Script ориентируется на текстовые сообщения в кодировке **UTF-8**.|
|ContentType|string|Тип содержимого тела сообщения.<br>Значение по умолчанию: **application/json**|
|ContentEncoding|string|Формат (кодировка) тела сообщения.<br>Значение по умолчанию: **UTF-8**|
|ReplyTo|string|Адресат для обратной связи, определяемый логикой приложения.|
|CorrelationId|string|Идентификатор корреляции сообщений между собой, определяемый логикой приложения.|
|Headers|object|Пользовательские заголовки (смотри документацию ниже)|

[Наверх](#команда-consume)

#### Пример кода DaJet Script

```SQL
DECLARE @message object -- Сообщение RabbitMQ

CONSUME 'amqp://guest:guest@localhost:5672/dajet'
   WITH QueueName = 'test-queue', Heartbeat = 10
   INTO @message

USE 'mssql://server/database'

   INSERT РегистрСведений.ВходящиеСообщения
   SELECT НомерСообщения = VECTOR('so_incoming_queue')
        , Отправитель    = @message.AppId
        , ТипСообщения   = @message.Type
        , ТелоСообщения  = @message.Body

END
```

[Наверх](#команда-consume)

#### Обработка пользовательских заголовков

```SQL
DECLARE @message object -- Сообщение RabbitMQ

CONSUME 'amqp://guest:guest@localhost:5672/dajet'
   WITH QueueName = 'test-queue', Heartbeat = 10
   INTO @message

USE 'mssql://server/database'

   INSERT РегистрСведений.ВходящиеСообщения
   SELECT НомерСообщения = VECTOR('so_incoming_queue')
        , Отправитель    = @message.AppId
        , ТипСообщения   = @message.Type
        , ТелоСообщения  = @message.Body

END
```

[Наверх](#команда-consume)
