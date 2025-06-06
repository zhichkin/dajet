## DaJet - язык интеграции

- [Общее описание](#общее-описание)
  - [Транспорт сообщений](#транспорт-сообщений)
  - [Трансформация данных](#трансформация-данных)
  - [Маршрутизация сообщений](#маршрутизация-сообщений)
- [Утилита выполнения скриптов](https://github.com/zhichkin/dajet/tree/main/doc/dajet-utility/README.md)
- [Http-сервер и редактор кода](https://github.com/zhichkin/dajet/tree/main/doc/dajet-studio/README.md)
- [Документация DaJet Script](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/README.md)
- **Полезные инструменты:**
  - [Анализ метаданных 1С:Предприятие 8](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/md-streamer/README.md)
  - [Диагностика чтения метаданных 1С:Предприятие 8](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/md-streamer/README.md#%D1%82%D0%B5%D1%81%D1%82-dajet-script-%D0%BD%D0%B0-%D1%87%D1%82%D0%B5%D0%BD%D0%B8%D0%B5-%D0%BC%D0%B5%D1%82%D0%B0%D0%B4%D0%B0%D0%BD%D0%BD%D1%8B%D1%85-1%D1%81%D0%BF%D1%80%D0%B5%D0%B4%D0%BF%D1%80%D0%B8%D1%8F%D1%82%D0%B8%D0%B5-8)
  - [Генерация представлений СУБД для 1С:Предприятие 8](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/db-view/README.md)

### Общее описание

DaJet Script – это расширяемый SQL-подобный язык программирования для организации обмена данными между узлами интеграции. Узлами интеграции в данном контексте называются реляционные базы данных, в том числе 1С:Предприятие 8, брокеры сообщений Apache Kafka или RabbitMQ, а также web api сервисы (в том числе веб-сервера DaJet).

Скрипты DaJet формируются из произвольного количества команд, которые используются средой выполнения как блоки для построения конвейера обработки и обмена данными. Имеются команды условного выполнения, а также вызова внешних скриптов, что делает язык DaJet полноценным процедурным языком программирования для решения широкого спектра задач.

Средой выполнения скриптов DaJet является платформа Microsoft .NET 8. Хостинг (регулярное) или одноразовое выполнение скриптов DaJet может осуществляться при помощи консольной [утилиты dajet](https://github.com/zhichkin/dajet/tree/main/doc/dajet-utility/README.md), специализированного [http-сервера](https://github.com/zhichkin/dajet/tree/main/doc/dajet-studio/README.md) или [программно](#пример-программного-выполнения-скрипта). При этом утилита dajet может использоваться в качестве сервиса Windows или демона Linux. Во втором случае любой скрипт может вызываться как web api метод http-сервера DaJet. Ну и, наконец, процессор скриптов DaJet предоставляет свой API на C#.

DaJet является расширяемым языком программирования. Точками расширения являются пользовательские функции и процессоры команд, которые могут быть созданы на языке C# путём реализации соответствующих интерфейсов.

Язык DaJet был реализован в том числе для поддержки [методики РИБ 2.0](https://infostart.ru/video/w2100189/)

[Наверх](#dajet---язык-интеграции)

### Транспорт сообщений

Процессор скриптов DaJet стремится обеспечить гарантию доставки сообщений, соответствующую уровню at least once in order. Другими словами гарантируется, что сообщения источника будут доставлены получателю хотя бы один раз именно в том порядке, который был определён при отправке, то есть соблюдая очередность FIFO.

Следующий простой скрипт DaJet демонстрирует потребление сообщений из таблицы-очереди (регистра сведений) базы данных источника и их доставку в информационную базу 1С:Предприятие 8 узла приёмника.

```TSQL
-- *************************************************************
-- * Источник SQL Server - регистр сведений "ИсходящаяОчередь" *
-- *************************************************************
USE 'mssql://server_address/source_database'

DECLARE @ПустаяСсылка uuid = '00000000-0000-0000-0000-000000000000'
DECLARE @ЭтотУзел   string = SELECT Код
                               FROM ПланОбмена.ПланОбменаДанными
                              WHERE Предопределённый <> @ПустаяСсылка
DECLARE @message object

CONSUME TOP 1000
        ТипСообщения, ТелоСообщения INTO @message
   FROM РегистрСведений.ИсходящаяОчередь
  ORDER BY НомерСообщения ASC

-- **************************************************************
-- * Приёмник PostgreSQL - регистр сведений "ВходящиеСообщения" *
–- **************************************************************
USE 'pgsql://postgres:postgres@127.0.0.1:5432/target_database'

INSERT РегистрСведений.ВходящиеСообщения
SELECT НомерСообщения = VECTOR('so_incoming_queue')
     , Отправитель    = @ЭтотУзел
     , ТипСообщения   = @message.ТипСообщения
     , ТелоСообщения  = @message.ТелоСообщения
     , ОтметкаВремени = NOW()

END -- Контекст базы данных приёмника
END -- Контекст базы данных источника
```

[Наверх](#dajet---язык-интеграции)

### Трансформация данных

Команды DaJet могут использоваться для трансформации данных или "конвертации данных" в терминах 1С:Предприятие. Например, следующий скрипт демонстрирует перенос данных из одной информационной базы 1С в другую, применяя трансформацию извлекаемых данных при помощи оператора APPEND и далее выполняя сериализацию сообщения в формат JSON.

В данном примере результатом соединения данных из справочника "Номенклатура" и регистра сведений "ЦеныНоменклатуры" является следующая структура данных в формате JSON:
```JSON
{
   "Ссылка": "8d400f9d-935c-8ecc-11ee-c2db228ea72a",
   "Код": "MS-01",
   "Наименование": "Товар 01",
   "Цены":
   [
      { "Период": "2023-01-01T00:00:00", "Цена": 100.00 },
      { "Период": "2023-01-02T00:00:00", "Цена": 123.00 },
      { "Период": "2023-01-03T00:00:00", "Цена": 321.00 }
   ]
}
```
```SQL
-- *************************************************************
-- * Источник сообщений SQL Server - справочник "Номенклатура" *
-- *************************************************************
USE 'mssql://server_address/source_database'

DECLARE @message object

SELECT Ссылка       = UUIDOF(Ссылка)
     , Код          = RTRIM(LTRIM(Код))
     , Наименование = SUBSTRING(Наименование, 1, 10)
  INTO @message
  FROM Справочник.Номенклатура
APPEND (SELECT Период, Цена
          FROM РегистрСведений.ЦеныНоменклатуры
         WHERE Номенклатура = @message.Ссылка
         ORDER BY Период ASC) AS Цены

-- **************************************************************
-- * Приёмник PostgreSQL - регистр сведений "ВходящиеСообщения" *
-- **************************************************************
USE 'pgsql://postgres:postgres@127.0.0.1:5432/target_database'

INSERT РегистрСведений.ВходящиеСообщения
SELECT НомерСообщения = VECTOR('so_incoming_queue')
     , Отправитель    = @message.Код
     , ТипСообщения   = 'Справочник.Номенклатура'
     , ТелоСообщения  = JSON(@message)
     , ОтметкаВремени = NOW()

END -- Контекст базы данных приёмника
END -- Контекст базы данных источника
```

[Наверх](#dajet---язык-интеграции)

### Маршрутизация сообщений

Маршрутизация сообщений между узлами получателями может осуществляться при помощи условных операторов языка DaJet, а также используя шаблоны адресации узлов интеграции. Например, в следующем примере обратите внимание на использование переменных @source и @request в командах USE и REQUEST соответственно. Текущие значения этих переменных, взятые из настроек скрипта или базы данных, будут использованы в процессе выполнения скрипта для подстановки в шаблонах адресации базы данных и web api: '{@source}' и '{@request.Адрес}' соответственно.

```SQL
DECLARE @sender   string = 'DaJet'
DECLARE @source   string = 'pgsql://postgres:postgres@localhost:5432/database_name'
DECLARE @request  object -- параметры http-запроса
DECLARE @response object -- код и тело http-ответа

-- ***************************************************************
-- * Источник HTTP запросов, регистр сведений "ИсходящиеЗапросы" *
-- ***************************************************************
USE '{@source}'

STREAM Идентификатор, Адрес, Метод, Тело, Попытка
  INTO @request
  FROM РегистрСведений.ИсходящиеЗапросы
 WHERE Статус  = Перечисление.СтатусыЗапросов.Новый
    OR Статус  = Перечисление.СтатусыЗапросов.Ошибка
   AND Попытка < 5

-- *********************************************************
-- * Синхронный вызов web API согласно заданным параметрам *
-- *********************************************************

REQUEST '{@request.Адрес}' -- URL метода web API
   WITH User-Agent   = @sender
      , Content-Type = 'application/json;charset=utf-8'
 SELECT OnError = 'continue'     -- continue или break
      , Method  = @request.Метод -- HTTP метод запроса
      , Content = @request.Тело  -- Тело HTTP запроса
   INTO @response -- { "Code": "200", "Value": "text" }

END -- Контекст базы данных источника
```

[Наверх](#dajet---язык-интеграции)

### Пример программного выполнения скрипта

```CSHARP
using DaJet.Stream;

namespace DaJet.Script.Services
{
    public class DaJetScriptExecutor
    {
        public object ExecuteScript(in string filePath)
        {
            if (StreamManager.TryExecute(in filePath, out object result, out string error))
            {
                return result;
            }

            throw new Exception(error);
        }
    }
}
```
[Канал DaJet в Телеграм](https://t.me/dajet_studio)

[Школа DaJet на YouTube](https://www.youtube.com/playlist?list=PLyBbhdsc7InutmVxyUszw-ZNJ5zKVBot2)

[Дополнительные материалы](https://zhichkin.github.io/)
