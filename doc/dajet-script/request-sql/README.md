## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Произвольные запросы СУБД на "родном" SQL

Начиная с версии **DaJet Script 3.9.8**, расширены возможности команды **REQUEST** для выполнения произвольных запросов к СУБД на соответствующем "родном" для неё диалекте SQL. Для возврата результата выполнения запроса SQL требуется использование команды [**DEFINE**](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/define/README.md), которая в данном случае используется как определение контракта данных между DaJet Script и СУБД. Ниже по ссылкам можно найти примеры использования команды **REQUEST** для каждой конкретной СУБД:
- [Sqlite](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/request-sql/sqlite/README.md)
- [SQL Server](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/request-sql/mssql/README.md)
- [PostgreSQL](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/request-sql/pgsql/README.md)

Для переключения команды **REQUEST** в режим выполнения произвольных запросов SQL необходимо в строке подключения после ```?``` добавить параметр ```sql```. Например вот так:

```
REQUEST 'pgsql://postgres:postgres@localhost:5432/database?sql'
```

> В данном варианте использования команда **REQUEST** не требует наличия команды **USE** для создания соответствующего контекста базы данных. Вызов скрипта SQL выполняется в собственном контексте, заданном строкой подключения.

Полный синтаксис команды **REQUEST**:
```
REQUEST 'url?sql'
   WITH <options>
[SELECT <input-parameters>]
  [INTO <output-variable>]
```
**\<url\>** - строка подключения к соответствующей СУБД.<br>
**\<options\>** - опции (настройки) выполнения скрипта SQL.<br>
**\<input-parameters\>** - параметры скрипта (запроса) SQL.<br>
**\<output-variable\>** - переменная типа ```object``` или ```array```, в которую возвращается результат выполнения запроса SQL.

**Таблица опций (настроек) команды REQUEST**
|**Параметр**|**Тип данных**|**Описание**|
|------------|--------------|------------|
|Script|string|Текст выполняемого запроса SQL, переменная строкового типа или ссылка на файл, который содержит скрипт SQL.|
|Timeout|number|Таймаут выполнения команды SQL в секундах. Значение по умолчанию: 30.|
|Transaction|string|Необязательная опция для выполнения команды SQL в транзакции. По умолчанию команда выполняется без открытия транзакции СУБД. Допустимые значения (разные СУБД поддерживают только те или иные значения):<br>- 'ReadUncommitted'<br>- 'ReadCommitted'<br>- 'RepeatableRead'<br>- 'Serializable'<br>- 'Snapshot'|
|Stream|boolean|Необязательная опция выполнения команды REQUEST в потоковом (TRUE) или обычном (FALSE) режиме. Значение по умолчанию: FALSE. |

> Более подробно про потоковые команды и их поведение можно посмотреть в соответствующей документации по командам [**STREAM**](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/databases/stream/README.md) или [**CONSUME**](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/databases/consume/README.md). В случае использования команды **REQUEST** в потоковом режиме в предложении **INTO** должна использоваться переменная типа ```object```.

[Наверх](#произвольные-запросы-субд-на-родном-sql)

**Визуальная схема сопоставления скриптов SQL и DaJet Script**
![Схема сопоставления скриптов SQL и DaJet Script](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-database-request-sql.png)
