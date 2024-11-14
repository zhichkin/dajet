## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Команда CONSUME

- [Общее описание](#общее-описание)
- Возможные приёмники данных
  - [База данных (таблица-очередь)]()
  - [База данных (хранимая процедура)]()
  - [RabbitMQ]()
  - [Apache Kafka]()
  - [Web API]()
  - [Пользовательский обработчик на C#]()
- Дополнительные материалы
  - [Методика РИБ 2.0](https://zhichkin.github.io/mp4/rib20.mp4) (видео mp4)
  - [Методика РИБ 2.0](https://zhichkin.github.io/distributed-info-bases-2-0.pdf) (слайды pdf)

#### Общее описание

Команда **CONSUME** реализована DaJet Script специально для работы с таблицами СУБД, которые используются как очереди. Это могут быть очереди сообщений, событий или заданий для асинхронной обработки данных, организации обмена данными или решения любых других аналогичных задач. В контексте 1С:Предприятие 8 такими таблицами-очередями могут быть, например, таблицы регистрации изменений планов обмена или регистры сведений исходящих сообщений. Использование команды **CONSUME** дополняется [механизмом управления последовательностью](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/sequence/README.md), а всё это, вместе взятое, призвано поддержать применение **методики РИБ 2.0** на практике.

```SQL
DECLARE @message object

USE 'mssql://sa:sa@localhost:1433/source-database'

   CONSUME TOP 10
           НомерСообщения, Заголовки
         , ТипСообщения, ТелоСообщения
      INTO @message
      FROM РегистрСведений.ИсходящиеСообщения
     ORDER BY НомерСообщения ASC
   
   USE 'pgsql://postgres:postgres@localhost:5432/target-database'

      INSERT РегистрСведений.ВходящиеСообщения
      SELECT Отправитель    = 'DaJet Script'
           , НомерСообщения = VECTOR('so_incoming_queue')
           , Заголовки      = @message.Заголовки
           , ТипСообщения   = @message.ТипСообщения
           , ТелоСообщения  = @message.ТелоСообщения

   END

END
```

![Схема выполнения команды CONSUME](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-database-consume-to-database.png)

![Схема выполнения команды CONSUME](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-database-consume-to-database-schema.png)

![Схема выполнения команды CONSUME](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-database-consume-to-rabbitmq.png)

![Схема выполнения команды CONSUME](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-database-consume-to-rabbitmq-schema.png)

[Наверх](#команда-consume)
