## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Объявление и использование переменных

Переменные DaJet Script имеют такое же назначение, как и в других общепринятых языках программирования. Они могут использоваться как сами по себе для вычисления выражений, так и в качестве параметров команд DaJet Script, значений их настроек, параметров запросов к базам данных, а также в строковых шаблонах адресов подключения и URL в качестве значений для подстановки. Более подробно использование переменных DaJet Script описано в соответствующих разделах документации.

- [Объявление](#объявление)
- [Инициализация](#инициализация)
- [Присваивание значения](#присваивание-значения)
- [Область видимости](#область-видимости)
- [Присваивание значения запросом](#присваивание-значения-запросом)
- [Тип ```entity```](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/entity/README.md)
- [Тип ```object```](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/object/README.md)
- [Тип ```array```](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/array/README.md)

### Объявление

Объявление переменных DaJet Script выполняется командой **DECLARE**. Объявление переменных может осуществляться в любом месте скрипта, однако при этом следует учитывать их область видимости. Общий синтаксис команды выглядит следующим образом:

```TSQL
DECLARE @<identifier> <type> [= <initializer>]
```
**\<identifier\>** - имя переменной.<br>
**\<type\>** - тип переменной (одно из допустимых ключевых слов).<br>
**\<initializer\>** - необязательный инициализатор: скалярное значение (литерал) или запрос СУБД.

> Переменным, объявленным без иницализации, присваиваются значения по умолчанию.

> Переменные типов ```object``` и ```array``` инициализируются результатами выполнения других команд.

В следующем примере объявляются переменные всех допустимых типов данных DaJet Script. Им присваиваются значения по умолчанию, которые в конечном итоге выводятся в лог программы командой **PRINT**.

```TSQL
DECLARE @boolean  boolean
DECLARE @decimal  number
DECLARE @integer  number
DECLARE @datetime datetime
DECLARE @string   string
DECLARE @binary   binary
DECLARE @uuid     uuid
DECLARE @entity   entity
DECLARE @object   object
DECLARE @array    array

PRINT 'boolean  = ' + @boolean
PRINT 'decimal  = ' + @decimal
PRINT 'integer  = ' + @integer
PRINT 'datetime = ' + @datetime
PRINT 'string   = ' + @string
PRINT 'binary   = ' + @binary
PRINT 'uuid     = ' + @uuid
PRINT 'entity   = ' + @entity
PRINT 'object   = ' + @object
PRINT 'array    = ' + @array
```
**Результат выполнения скрипта**
```
[2024-09-30 17:52:37] boolean  = false
[2024-09-30 17:52:37] decimal  = 0.00
[2024-09-30 17:52:37] integer  = 0.00
[2024-09-30 17:52:37] datetime = 0001-01-01 00:00:00
[2024-09-30 17:52:37] string   =    -- Это комментарий: пустая строка
[2024-09-30 17:52:37] binary   = 0x -- Тоже комментарий: пустой массив байт
[2024-09-30 17:52:37] uuid     = 00000000-0000-0000-0000-000000000000
[2024-10-01 17:52:37] entity   = {0:00000000-0000-0000-0000-000000000000}
[2024-09-30 17:52:37] object   = null
[2024-09-30 17:52:37] array    = null
```

Важно отметить, что универсальный числовой тип данных ```number``` по умолчанию инициализируется десятичным числом ```0.00```. В некоторых случаях это имеет значение. Например, функция баз данных **COUNT** возвращает целое число. Для того, чтобы инициализировать ```number``` целочисленным типом ```integer```, в отличие от десятичного ```decimal```, следует использовать литерал числа без десятичной точки. Например вот так:

```TSQL
DECLARE @integer number = 0    -- Это целое число
DECLARE @decimal number = 0.00 -- Это десятичное число
```
Более наглядный пример сравнения двух чисел:
```TSQL
DECLARE @integer number = 0
DECLARE @decimal number = 0.00

IF @integer = 0
   THEN PRINT '@integer равно 0'
   ELSE PRINT '@integer не равно 0'
END

IF @decimal = 0
   THEN PRINT '@decimal равно 0'
   ELSE PRINT '@decimal не равно 0'
END

IF @integer = @decimal
   THEN PRINT '@integer равно @decimal'
   ELSE PRINT '@integer не равно @decimal'
END
```
**Результат выполнения скрипта**
```
[2024-09-30 20:38:13] @integer равно 0
[2024-09-30 20:38:13] @decimal не равно 0
[2024-09-30 20:38:13] @integer не равно @decimal
```
[Наверх](#объявление-и-использование-переменных)

### Инициализация

Инициализация переменных может выполняться при помощи оператора присваивания ```=``` (знак "равно") непосредственно в команде объявления переменной **DECLARE**. Следующий пример демонстрирует это, используя допустимые DaJet Script соответствующие литералы.

```TSQL
DECLARE @boolean  boolean  = true
DECLARE @decimal  number   = 12.34
DECLARE @integer  number   = 12345
DECLARE @datetime datetime = '2022-08-01T12:34:56'
DECLARE @string   string   = 'Это тестовая строка'
DECLARE @binary   binary   = 0x001234567890ABCDEF
DECLARE @uuid     uuid     = 'ba1f9927-0aec-11ed-9cd3-408d5c93cc8e'
DECLARE @entity   entity   = {12:e3aed142-9dbf-11ed-9ce2-408d5c93cc8e}
DECLARE @object   object   -- Литерал не определён
DECLARE @array    array    -- Литерал не определён

PRINT 'boolean  = ' + @boolean
PRINT 'decimal  = ' + @decimal
PRINT 'integer  = ' + @integer
PRINT 'datetime = ' + @datetime
PRINT 'string   = ' + @string
PRINT 'binary   = ' + @binary
PRINT 'uuid     = ' + @uuid
PRINT 'entity   = ' + @entity
PRINT 'object   = ' + @object
PRINT 'array    = ' + @array
```
**Результат выполнения скрипта**
```
[2024-09-30 17:46:19] boolean  = true
[2024-09-30 17:46:19] decimal  = 12.34
[2024-09-30 17:46:19] integer  = 12345
[2024-09-30 17:46:19] datetime = 2022-08-01 12:34:56
[2024-09-30 17:46:19] string   = Это тестовая строка
[2024-09-30 17:46:19] binary   = 0x001234567890abcdef
[2024-09-30 17:46:19] uuid     = ba1f9927-0aec-11ed-9cd3-408d5c93cc8e
[2024-09-30 17:46:19] entity   = {12:e3aed142-9dbf-11ed-9ce2-408d5c93cc8e}
[2024-09-30 17:46:19] object   = null
[2024-09-30 17:46:19] array    = null
```
[Наверх](#объявление-и-использование-переменных)

### Присваивание значения

Присваивание значений переменным выполняется при помощи команды **SET**. Переменной может быть присвоено скалярное значение, выраженное литералом, значение другой переменной, результат вычисления выражения или выполнения запроса к базе данных. Общий синтаксис команды выглядит следующим образом:
```TSQL
SET @<identifier> = <expression>
```
**\<identifier\>** - имя переменной, как объявлено командой **DECLARE**.<br>
**\<expression\>** - скалярное значение (литерал), выражение или запрос СУБД.
```TSQL
DECLARE @text string
DECLARE @name string = 'Мир'
SET @text = 'Привет' + ', ' + @name + '!'
PRINT @text
```
**Результат выполнения скрипта**
```
[2024-09-30 20:54:39] Привет, Мир!
```
[Наверх](#объявление-и-использование-переменных)

### Область видимости

DaJet Script имеет разные области видимости для переменных. Область видимости переменной ограничивается тем блоком команд, в котором она была объявлена. Таким образом области видимости образуют древовидную иерархическую структуру. При этом из вложенной области видимости можно видеть родительские, но не наоборот. Блоки команд являются составной частью таких команд, как, например, команда **IF**.
```TSQL
IF <условие> THEN <блок команд> [ELSE <блок команд>] END

-- <блок команд> - это отдельная область видимости для переменных
```

Лучше всего, чтобы понять как это работает, показать на примере.
```TSQL
-- Этот скрипт выполнится с ошибкой при выполнении последней команды PRINT @scoped,
-- так как в корневой области видимости выполняется обращение к переменной @scoped,
-- которая объявлена в области видимости внутреннего блока THEN команды IF

DECLARE @root string = 'Корневая область видимости'

IF TRUE THEN
   DECLARE @scoped string
   SET @scoped = '[IF] Область блока THEN'
END

PRINT @scoped
```
**Результат выполнения скрипта**
```
[2024-09-30 21:47:00] Variable [@scoped] is not found
```
Следующий скрипт выполнится без ошибок:
```TSQL
DECLARE @root string = 'Корневая область видимости'

IF TRUE THEN
   DECLARE @scoped string
   SET @scoped = '[IF] Область блока THEN'
   PRINT '[IF] вижу @root   = ' + @root
   PRINT '[IF] вижу @scoped = ' + @scoped
END

PRINT '[SCRIPT] вижу @root = ' + @root
```
**Результат выполнения скрипта**
```
[2024-09-30 22:02:17] [IF] вижу @root = Корневая область видимости
[2024-09-30 22:02:17] [IF] вижу @scoped = [IF] Область блока THEN
[2024-09-30 22:02:17] [SCRIPT] вижу @root = Корневая область видимости
```

> В общем случае рекомендуется объявлять все необходимые переменные в корневой области видимости скрипта, то есть в первых строках его кода.

[Наверх](#объявление-и-использование-переменных)

### Присваивание значения запросом

Переменным DaJet Script могут присваиваться значения, которые являются результатом выполнения запроса к базе данных. Существует три способа сделать это, которые мы рассмотрим далее. Очевидно, что такое присваивание всегда выполняется в контексте соответствующей базы данных. Для того, чтобы открыть контекст работы с нужной базой данных используется команда **USE**. Общий синтаксис этой команды выглядит следующим образом:
```TSQL
USE '<uri>' <statements> END
```
**\<uri\>** - строка подключения к базе данных в формате URI или строковой шаблон.<br>
**\<statements\>** - блок команд DaJet Script, имеющий собственную область видимости.

Далее приводятся примеры кода DaJet Script, которые показывают три способа присваивания значений переменным запросами СУБД:
- [Инициализатор команды **DECLARE**](#1-инициализатор-команды-declare)
- [Выражение присваивания команды **SET**](#2-выражение-присваивания-команды-set)
- [Запрос вида **SELECT** ... **INTO** @variable](#3-запрос-вида-select--into-variable)

При этом следует обратить внимание, что каждый способ имеет три разновидности:
- Присваивание скалярного значения простого типа
- Присваивание значения типа ```object``` (один объект)
- Присваивание значения типа ```array``` (массив объектов)

Для визуализации результата работы скриптов в некоторых случаях используется сериализация в формат JSON при помощи одноимённой встроенной функции DaJet Script.

#### 1. Инициализатор команды DECLARE

```TSQL
-- Присваивание скалярного значения простого типа

DECLARE @code string = '00000001'

USE 'mssql://server/database'
   DECLARE @entity entity = SELECT TOP 1 Ссылка
                              FROM Справочник.Номенклатура
                             WHERE Код = @code
                               AND ПометкаУдаления = false
   PRINT '@entity = ' + @entity
END

-- Результат выполнения скрипта
[2024-10-01 10:58:51] @entity = {36:08ec109d-a06b-a1b1-11ee-ca472bff0a0d}
```

```TSQL
-- Присваивание значения типа object (один объект)

DECLARE @code string = '00000001'

USE 'mssql://server/database'
   DECLARE @object object = SELECT TOP 1 Код
                                 , Наименование
                              FROM Справочник.Номенклатура
                             WHERE Код = @code
                               AND ПометкаУдаления = false
   PRINT JSON(@object)
END

-- Результат выполнения скрипта
-- Значение переменной @object сериализовано в JSON
[2024-10-01 11:09:27] {"Код":"00000001","Наименование":"Товар 1"}
```

```TSQL
-- Присваивание значения типа array (массив объектов)

USE 'mssql://server/database'
   DECLARE @array array = SELECT TOP 2
                                 code = Код
                               , name = Наименование
                            FROM Справочник.Номенклатура
                           WHERE ПометкаУдаления = false
   PRINT JSON(@array)
END

-- Результат выполнения скрипта
-- Значение переменной @array сериализовано в JSON
[2024-10-01 11:14:26] [{"code":"00000001","name":"Товар 1"},{"code":"00000002","name":"Товар 2"}]
```
[Наверх](#присваивание-значения-запросом)

#### 2. Выражение присваивания команды SET

```TSQL
-- Присваивание скалярного значения простого типа

DECLARE @count number

USE 'mssql://server/database'
   SET @count = SELECT COUNT(Ссылка)
                  FROM Справочник.Номенклатура
                 WHERE ПометкаУдаления = false
END

PRINT '@count = ' + @count

-- Результат выполнения скрипта
[2024-10-01 11:40:09] @count = 10000
```

```TSQL
-- Присваивание значения типа object (один объект)

DECLARE @object object
DECLARE @code   string = '00000001'

USE 'mssql://server/database'
   SET @object = SELECT TOP 1 Код
                      , Наименование
                   FROM Справочник.Номенклатура
                  WHERE Код = @code
                    AND ПометкаУдаления = false
END

PRINT JSON(@object)

-- Результат выполнения скрипта
-- Значение переменной @object сериализовано в JSON
[2024-10-01 11:43:23] {"Код":"00000001","Наименование":"Товар 1"}
```

```TSQL
-- Присваивание значения типа array (массив объектов)

DECLARE @array array

USE 'mssql://server/database'
    SET @array = SELECT TOP 2
                        code = Код
                      , name = Наименование
                   FROM Справочник.Номенклатура
                  WHERE ПометкаУдаления = false
END

PRINT JSON(@array)

-- Результат выполнения скрипта
-- Значение переменной @array сериализовано в JSON
[2024-10-01 11:45:33] [{"code":"00000001","name":"Товар 1"},{"code":"00000002","name":"Товар 2"}]
```
[Наверх](#присваивание-значения-запросом)

#### 3. Запрос вида SELECT ... INTO @variable

```TSQL
-- Присваивание скалярного значения простого типа

DECLARE @count number

USE 'mssql://server/database'
   SELECT COUNT(Ссылка)
     INTO @count
     FROM Справочник.Номенклатура
    WHERE ПометкаУдаления = false
END

PRINT '@count = ' + @count

-- Результат выполнения скрипта
[2024-10-01 11:47:59] @count = 10000
```

```TSQL
-- Присваивание значения типа object (один объект)

DECLARE @object object
DECLARE @code   string = '00000001'

USE 'mssql://server/database'
   SELECT TOP 1 Код
        , Наименование
     INTO @object
     FROM Справочник.Номенклатура
    WHERE Код = @code
      AND ПометкаУдаления = false
END

PRINT JSON(@object)

-- Результат выполнения скрипта
-- Значение переменной @object сериализовано в JSON
[2024-10-01 11:49:35] {"Код":"00000001","Наименование":"Товар 1"}
```

```TSQL
-- Присваивание значения типа array (массив объектов)

DECLARE @array array

USE 'mssql://server/database'
    SELECT TOP 2
           code = Код
         , name = Наименование
      INTO @array
      FROM Справочник.Номенклатура
     WHERE ПометкаУдаления = false
END

PRINT JSON(@array)

-- Результат выполнения скрипта
-- Значение переменной @array сериализовано в JSON
[2024-10-01 11:51:23] [{"code":"00000001","name":"Товар 1"},{"code":"00000002","name":"Товар 2"}]
```
[Наверх](#присваивание-значения-запросом)
