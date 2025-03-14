## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Apache Kafka

- [Отправка сообщений в топик Apache Kafka](#отправка-сообщений-в-топик-apache-kafka)
- [Получение сообщений из топика Apache Kafka](#получение-сообщений-из-топика-apache-kafka)

**DaJet Script** реализует работу с топиками **Apache Kafka** при помощи двух команд: **CONSUME** и **PRODUCE**. Концепция их работы аналогична [одноимённым командам RabbitMQ](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/rabbitmq/README.md).

> **Внимание!** Специальной строки подключения не используется! Указывается только идентификатор адаптера: **kafka**.

Реализация DaJet Script для работы с **Apache Kafka** имеет самый простой из возможных вариантов. Предполагается работа только с текстовыми значениями в кодировке ```UTF-8```. Работа с ```protobuf``` или иными бинарными форматами не реализована по причине малого спроса (обращений) со стороны пользователей DaJet.

Как обычно гарантируется доставка сообщений на уровне **at-least-once-in-order** (минимум один раз строго по порядку). Дублирование (повторную отправку) сообщений DaJet Script не контролирует. Для этого следует использовать, либо логику приложения, либо возможности самого брокера **Apache Kafka**.

Ниже следующие скрипты DaJet Script демонстрируют основные сценарии использования адаптера **Apache Kafka** при помощи соответствующих команд **CONSUME** и **PRODUCE**, используя возможные, но не обязательные для всех случаев, варианты настройки.

Настройку адаптера для конкретного сценария следует выполнять согласно официальной документации:
- [Настройка команды PRODUCE - продюсер Apache Kafka](https://docs.confluent.io/platform/current/clients/producer.html)
- [Настройка команды CONSUME - консюмер Apache Kafka](https://docs.confluent.io/platform/current/clients/consumer.html)

**Таблица свойств записей (сообщений) Apache Kafka**

|**Свойство**|**Тип данных**|**Описание**|
|---|---|---|
|Key|string|Ключ записи|
|Value|string|Тело записи|
|Topic|string|Наименование топика|

> **Важно!** Предполагается использование текстовых значений в кодировке ```UTF-8```.

[Наверх](#apache-kafka)

#### Отправка сообщений в топик Apache Kafka

```SQL
DECLARE @changes object -- Запись таблицы регистрации изменений
DECLARE @message object -- Данные элемента справочника "Номенклатура"

-- ********************************************************************************
-- * Источник сообщений, таблица регистрации изменений справочника "Номенклатура" *
-- ********************************************************************************
USE 'mssql://server/database'

   DECLARE @Получатель entity = SELECT Ссылка
                                  FROM ПланОбмена.ПланОбменаРИБ
                                 WHERE Код = 'KAFKA'
                                   AND ПометкаУдаления = false

   CONSUME TOP 1000 Ссылка INTO @changes
      FROM Справочник.Номенклатура.Изменения -- Таблица регистрации изменений
     WHERE УзелОбмена = @Получатель          -- плана обмена 1С:Предприятие 8
     ORDER BY Ссылка ASC

    SELECT Ссылка       = UUIDOF(Ссылка)
         , Код          = LTRIM(RTRIM(Данные.Код))
         , Наименование = Данные.Наименование
      INTO @message
      FROM Справочник.Номенклатура
     WHERE Ссылка = @changes.Ссылка

   -- *******************************************************
   -- * Приёмник сообщений Apache Kafka, топик "test-topic" *
   -- *******************************************************
   PRODUCE 'kafka'
      WITH acks               = 'all'
         , client.id          = 'dajet'
         , max.in.flight      = 1
         , bootstrap.servers  = '192.168.238.182:9092' --  (csv)
         , message.timeout.ms = 30000
         , enable.idempotence = false
    SELECT Key   = 'Справочник.Номенклатура' -- Ключ записи
         , Value = JSON(@message)            -- Тело сообщения
         , Topic = 'test-topic'              -- Топик Apache Kafka

END -- Контекст базы данных источника
```

[Наверх](#apache-kafka)

#### Получение сообщений из топика Apache Kafka

```SQL
DECLARE @message object -- Запись (сообщение) Apache Kafka

-- *****************************************************
-- * Источник сообщений Apache Kafka, топик test-topic *
-- *****************************************************
CONSUME 'kafka'
   WITH topic     = 'test-topic'
      , group.id  = 'dajet'
      , client.id = 'dajet'
      , bootstrap.servers     = '192.168.239.177:9092' -- csv
      , enable.auto.commit    = false
      , auto.offset.reset     = 'earliest'
      , session.timeout.ms    = 60000
      , heartbeat.interval.ms = 20000
   INTO @message

-- ************************************************************************
-- * Приёмник сообщений SQL Server - регистр сведений "ВходящиеСообщения" *
-- ************************************************************************
USE 'mssql://server/database'

   INSERT РегистрСведений.ВходящиеСообщения
   SELECT НомерСообщения = VECTOR('so_incoming_queue')
        , ОтметкаВремени = NOW()
        , Отправитель    = @message.Key
        , ТипСообщения   = @message.Topic
        , ТелоСообщения  = @message.Value

END -- Контекст базы данных приёмника
```

[Наверх](#apache-kafka)
