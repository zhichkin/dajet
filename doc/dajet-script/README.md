## DaJet Script

- [Общее описание](#общее-описание)
- [Система типов данных](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/%D0%A1%D0%B8%D1%81%D1%82%D0%B5%D0%BC%D0%B0%20%D1%82%D0%B8%D0%BF%D0%BE%D0%B2%20%D0%B4%D0%B0%D0%BD%D0%BD%D1%8B%D1%85/README.md)
  - [Тип ```entity``` (ссылка)](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/entity/README.md)
  - [Тип ```object``` (структура данных)](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/object/README.md)
  - [Тип ```array``` (массив структур)](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/array/README.md)
- [Объявление и использование переменных](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/%D0%9E%D0%B1%D1%8A%D1%8F%D0%B2%D0%BB%D0%B5%D0%BD%D0%B8%D0%B5%20%D0%B8%20%D0%B8%D1%81%D0%BF%D0%BE%D0%BB%D1%8C%D0%B7%D0%BE%D0%B2%D0%B0%D0%BD%D0%B8%D0%B5%20%D0%BF%D0%B5%D1%80%D0%B5%D0%BC%D0%B5%D0%BD%D0%BD%D1%8B%D1%85/README.md)
- [Алгоритмические возможности](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/algorithm/README.md)
- Функция JSON
- [Вызов скриптов по http](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/http/README.md)
- [Вызов внешних скриптов](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/%D0%92%D1%8B%D0%B7%D0%BE%D0%B2%20%D0%B2%D0%BD%D0%B5%D1%88%D0%BD%D0%B8%D1%85%20%D1%81%D0%BA%D1%80%D0%B8%D0%BF%D1%82%D0%BE%D0%B2/README.md)
  - EXECUTE ```script.djs```
- Базы данных
  - [USE (контекст базы данных)](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/use/README.md)
  - SELECT
    - Табличные операторы
      - JOIN, UNION, APPLY
      - Подзапросы
    - Общие табличные выражения
      - Обычные и рекурсивные запросы
    - Временные таблицы
    - Агрегатные функции
      - SUM, MAX, MIN, AVG, COUNT, STRING_AGG
    - Оконные функции OVER
      - SUM, MAX, MIN, AVG, COUNT
      - ROW_NUMBER, LAG, LEAD, FIRST_VALUE, LAST_VALUE
    - Строковые функции
      - LTRIM, RTRIM, LOWER, UPPER, REPLACE, CONCAT, CONCAT_WS, SUBSTRING, CHARLENGTH
    - Прочие функции и ключевые слова
      - ISNULL, NOW, NEWUUID, DATALENGTH
      - IN, LIKE, BETWEEN, = ANY/ALL, EXISTS
      - ORDER BY...OFFSET...FETCH...
  - STREAM (потоковый SELECT)
  - CONSUME (деструктивный STREAM)
  - INSERT
  - UPDATE (обычный и потоковый)
  - DELETE
  - REQUEST (хранимые процедуры)
  - [Управление последовательностью](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/sequence/README.md)
- RabbitMQ
  - CONSUME
  - PRODUCE
- Apache Kafka
  - CONSUME
  - PRODUCE
- RPC (http, web api)
  - REQUEST
- Программное расширение DaJet Script
  - PROCESS \<variables\> WITH \<c-sharp-interface\> INTO \<variable\> [SELECT \<options\>]
- [Примеры DaJet Script](https://github.com/zhichkin/dajet/tree/main/doc/dajet-utility/scripts)

### Общее описание

**DaJet Script** - это расширяемый SQL-подобный язык программирования для организации обмена данными между узлами интеграции. Скрипты DaJet это обыкновенные текстовые файлы с расширением ```djs``` в кодировке ```UTF-8```.

**Узлы интеграции** - это реляционные базы данных, в том числе 1С:Предприятие 8, брокеры сообщений Apache Kafka или RabbitMQ, сервисы web api и прочие источники данных.

**Скрипт DaJet** - это, по сути, отдельная процедура или функция. Код скрипта формируется из произвольного количества команд, которые используются средой выполнения как блоки для построения конвейера обработки и обмена данными. Имеются команды условного ветвления, циклического и параллельного выполнения кода, а также вызова внешних скриптов, что делает DaJet Script полноценным процедурным языком программирования для решения широкого круга задач.

DaJet Script является расширяемым языком программирования. Точками расширения являются пользовательские функции и процессоры данных (команда PROCESS), которые могут быть разработаны на языке C# путём реализации соответствующих классов или функций.

Средой выполнения DaJet Script является платформа Microsoft .NET 8. Выполнение скриптов DaJet может осуществляться при помощи консольной утилиты [dajet](https://github.com/zhichkin/dajet/tree/main/doc/dajet-utility/README.md), специализированного [http-сервера](https://github.com/zhichkin/dajet/tree/main/doc/dajet-studio/README.md) или [программно](https://github.com/zhichkin/dajet/blob/main/src/dajet/Program.cs). Любой скрипт можно вызвать как web api метод http-сервера DaJet. Утилита dajet может использоваться как сервис Windows или демон Linux (поддерживается systemd) для регулярного выполнения скриптов (хостинга). Ну и, наконец, процессор скриптов DaJet предоставляет удобный API на C#, что позволяет интегрировать его практически в любую программную оболочку.

[Наверх](#dajet-script)
