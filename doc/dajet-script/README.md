## DaJet Script

- [Общее описание](#общее-описание)
- [Система типов данных](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/data-type-system/README.md)
  - [Тип ```entity``` (ссылка)](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/entity/README.md)
  - [Тип ```object``` (структура данных)](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/object/README.md)
  - [Тип ```array``` (массив структур)](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/array/README.md)
- [Объявление и использование переменных](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/variables/README.md)
- [Алгоритмические возможности](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/algorithm/README.md)
- [Функция JSON](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/json/README.md)
- [Вызов скриптов по http](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/http/README.md)
- [Вызов внешних скриптов](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/execute/README.md)
  - EXECUTE ```script.djs```
- **Базы данных (mssql + pgsql)**
  - [USE (контекст базы данных)](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/use/README.md)
  - [Язык запросов DaJet Script](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/databases/README.md)
  - [Управление последовательностью](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/sequence/README.md)
  - [Анализ метаданных 1С:Предприятие 8](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/md-streamer/README.md)
- [REQUEST](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/request/README.md) (http, web api)
- [RabbitMQ](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/rabbitmq/README.md)
- [Apache Kafka](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/kafka/README.md)
- [Программное расширение DaJet Script](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/process/README.md)
- [Готовые шаблоны скриптов DaJet Script](https://github.com/zhichkin/dajet/tree/main/doc/dajet-utility/scripts)

### Общее описание

**DaJet Script** - это расширяемый SQL-подобный язык программирования для организации обмена данными между узлами интеграции. Скрипты DaJet это обыкновенные текстовые файлы с расширением ```djs``` в кодировке ```UTF-8```.

**Узлы интеграции** - это реляционные базы данных, в том числе 1С:Предприятие 8, брокеры сообщений Apache Kafka или RabbitMQ, сервисы web api и прочие источники данных.

**Скрипт DaJet** - это, по сути, отдельная процедура или функция. Код скрипта формируется из произвольного количества команд, которые используются средой выполнения как блоки для построения конвейера обработки и обмена данными. Имеются команды условного ветвления, циклического и параллельного выполнения кода, а также вызова внешних скриптов, что делает DaJet Script полноценным процедурным языком программирования для решения широкого круга задач.

DaJet Script является расширяемым языком программирования. Точками расширения являются пользовательские функции и процессоры данных (команда PROCESS), которые могут быть разработаны на языке C# путём реализации соответствующих классов или функций.

Средой выполнения DaJet Script является платформа Microsoft .NET 8. Выполнение скриптов DaJet может осуществляться при помощи консольной утилиты [dajet](https://github.com/zhichkin/dajet/tree/main/doc/dajet-utility/README.md), специализированного [http-сервера](https://github.com/zhichkin/dajet/tree/main/doc/dajet-studio/README.md) или [программно](https://github.com/zhichkin/dajet/blob/main/src/dajet/Program.cs). Любой скрипт можно вызвать как web api метод http-сервера DaJet. Утилита dajet может использоваться как сервис Windows или демон Linux (поддерживается systemd) для регулярного выполнения скриптов (хостинга). Ну и, наконец, процессор скриптов DaJet предоставляет удобный API на C#, что позволяет интегрировать его практически в любую программную оболочку.

[Наверх](#dajet-script)
