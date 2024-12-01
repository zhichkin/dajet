## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### RabbitMQ
- [CONSUME](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/rabbitmq/consume/README.md)
- [PRODUCE](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/rabbitmq/produce/README.md)

**DaJet Script** реализует работу с **RabbitMQ** при помощи двух команд: **CONSUME** и **PRODUCE**. В данном случае речь идёт о работе с обычными очередями RabbitMQ, а не потоками сообщений [RabbitMQ Streams](https://www.rabbitmq.com/docs/streams). 

Команда **CONSUME** создаёт [потребителя](https://www.rabbitmq.com/docs/consumers), который работает аналогично потоковым командам для баз данных [STREAM](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/databases/stream/README.md) и [CONSUME](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/databases/consume/README.md). То есть создаётся бесконечный замкнутый цикл получения сообщений от брокера RabbitMQ. Далее эти сообщения передаются следующим по конвейеру DaJet Script командам для обработки.

Команда **CONSUME** работает с брокером RabbitMQ по принципу **PUSH** от сервера. То есть клиент DaJet Script устанавливает подключение к брокеру и ждёт от него сообщений. Как только сообщения появляются в соответствующей очереди брокера, они сразу же отправляются клиенту. Командой **CONSUME** реализуется механизм подтверждения полученных от брокера сообщений (consumer aknowledgements). Это очень важно с точки зрения обеспечения надёжного обмена данными без потери сообщений.

Команда **PRODUCE** выполняет функцию [публикации](https://www.rabbitmq.com/docs/publishers) сообщений в брокере RabbitMQ. Команда **PRODUCE** является "непрозрачной", то есть замыкает конвейер обработки сообщений/данных и не передаёт их далее по конвейеру DaJet Script. Использование команды **PRODUCE** проектировалось в основном для совместного использования с командами баз данных [STREAM](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/databases/stream/README.md) и [CONSUME](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/databases/consume/README.md).

Команда **PRODUCE** отправляет сообщения в очередь или топик брокера RabbitMQ асинхронно. Сообщения отправляются пакетами, а затем выполняется ожидание подтверждения получения этих сообщений от брокера (publisher confirms). Это, также как и в случае с потреблением сообщений, очень важный механизм с точки зрения обеспечения надёжного обмена данными без потери сообщений.

> **Важно!** Более подробно про [механизмы подтверждения сообщений](https://www.rabbitmq.com/docs/confirms) можно и нужно прочитать в официальной документации RabbitMQ.

> **Важно!** Для более глубокого понимания (на уровне разработчика) необходимо знать и понимать особенности работы [клиента .NET для RabbitMQ](https://www.rabbitmq.com/client-libraries/dotnet-api-guide), так как DaJet Script использует именно его.

Обе команды имеют аналогичную строку подключения к брокеру RabbitMQ.

```
'amqp://<username>:<password>@<server>:<port>/<virtual-host>'
```
**\<username\>** - имя пользователя.<br>
**\<password\>** - пароль пользователя.<br>
**\<server\>** - адрес сервера RabbitMQ.<br>
**\<port\>** - порт сервера RabbitMQ (по умолчанию 5672).<br>
**\<virtual-host\>** - виртуальный хост сервера RabbitMQ (по умолчанию ```/```).<br>

> Строка подключения к брокеру RabbitMQ указывается в формате URL, следовательно, все специфические символы, например в пароле пользователя, должны быть указаны в URL-кодировке.

**Пример строки подключения к вирутальному хосту RabbitMQ по умолчанию**
```
'amqp://guest:guest@localhost:5672/%2F'
```

[Наверх](#rabbitmq)
