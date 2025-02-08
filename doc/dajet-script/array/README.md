## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Тип ```array```

Тип данных ```array``` реализован DaJet Script в основном для работы с выборками из баз данных, а также для циклической и параллельной обработки данных при помощи команды **FOR**. Параллельная обработка данных рассмотрена в соответствующем разделе документации DaJet Script.

> Начиная с версии DaJet Script 3.9.2, реализованы дополнительные функции для программной работы с типом ```array```.

- [Создание ```array``` запросом к базе данных](#создание-array-запросом-к-базе-данных)
- [Создание ```array``` при помощи функции JSON](#создание-array-при-помощи-функции-json)
- [Функции для работы с типом ```array```](#функции-для-работы-с-типом-array)
  - ARRAY_COUNT
  - ARRAY_CREATE
  - ARRAY_APPEND
  - ARRAY_SELECT
  - ARRAY_DELETE
  - ARRAY_INSERT

#### Создание ```array``` запросом к базе данных

```TSQL
-- 1. Создание массива объектов командой DECLARE

DECLARE @object object -- Объект для представления одной записи

USE 'mssql://server/database'

   -- Получаем массив объектов из базы данных запросом
   DECLARE @array array = SELECT TOP 5
                                 code = Код
                               , name = Наименование
                            FROM Справочник.Номенклатура

   -- Обработка массива объектов в цикле
   FOR @object IN @array
      PRINT JSON(@object)
   END

END

-- Результат выполнения скрипта
[2024-10-04 19:49:51] {"code":"00000001","name":"Товар 1"}
[2024-10-04 19:49:51] {"code":"00000002","name":"Товар 2"}
[2024-10-04 19:49:51] {"code":"00000003","name":"Товар 3"}
[2024-10-04 19:49:51] {"code":"00000004","name":"Товар 4"}
[2024-10-04 19:49:51] {"code":"00000005","name":"Товар 5"}
```

```TSQL
-- 2. Создание массива объектов командой SET

DECLARE @array  array  -- Массив для выборки записей из базы данных
DECLARE @object object -- Объект для представления одной записи выборки

USE 'mssql://server/database'

   -- Получаем массив объектов из базы данных запросом
   SET @array = SELECT TOP 5
                       code = Код
                     , name = Наименование
                  FROM Справочник.Номенклатура

   -- Обработка массива объектов в цикле
   FOR @object IN @array
      PRINT JSON(@object)
   END

END

-- Результат выполнения скрипта
[2024-10-04 19:54:26] {"code":"00000001","name":"Товар 1"}
[2024-10-04 19:54:26] {"code":"00000002","name":"Товар 2"}
[2024-10-04 19:54:26] {"code":"00000003","name":"Товар 3"}
[2024-10-04 19:54:26] {"code":"00000004","name":"Товар 4"}
[2024-10-04 19:54:26] {"code":"00000005","name":"Товар 5"}
```

```TSQL
-- 3. Создание массива объектов командой SELECT ... INTO

DECLARE @array  array  -- Массив для выборки записей из базы данных
DECLARE @object object -- Объект для представления одной записи выборки

USE 'mssql://server/database'

   -- Получаем массив объектов из базы данных запросом
   SELECT TOP 5
          code = Код
        , name = Наименование
     INTO @array
     FROM Справочник.Номенклатура

   -- Обработка массива объектов в цикле
   FOR @object IN @array
      PRINT JSON(@object)
   END

END

-- Результат выполнения скрипта
[2024-10-04 19:57:21] {"code":"00000001","name":"Товар 1"}
[2024-10-04 19:57:21] {"code":"00000002","name":"Товар 2"}
[2024-10-04 19:57:21] {"code":"00000003","name":"Товар 3"}
[2024-10-04 19:57:21] {"code":"00000004","name":"Товар 4"}
[2024-10-04 19:57:21] {"code":"00000005","name":"Товар 5"}
```

[Наверх](#тип-array)

#### Создание ```array``` при помощи функции JSON

```TSQL
DECLARE @array  array
DECLARE @object object

DECLARE @json string = '[
  { "name": "name 1", "value": 123 },
  { "name": "name 2", "value": 321 },
  { "name": "name 3", "value": 333 }
]'

-- Преобразование JSON в массив объектов
SET @array = JSON(@json)

PRINT 'Вывод результата в формате JSON'

FOR @object IN @array
   -- Преобразование object в JSON
   PRINT JSON(@object)
END

PRINT 'Вывод результата в формате TEXT'

FOR @object IN @array
   PRINT 'Свойство "name"  = ' + @object.name
   PRINT 'Свойство "value" = ' + @object.value
END

-- Результат выполнения скрипта
[2024-10-04 20:16:43] Вывод результата в формате JSON
[2024-10-04 20:16:43] {"name":"name 1","value":123}
[2024-10-04 20:16:43] {"name":"name 2","value":321}
[2024-10-04 20:16:43] {"name":"name 3","value":333}
[2024-10-04 20:16:43] Вывод результата в формате TEXT
[2024-10-04 20:16:43] Свойство "name"  = name 1
[2024-10-04 20:16:43] Свойство "value" = 123
[2024-10-04 20:16:43] Свойство "name"  = name 2
[2024-10-04 20:16:43] Свойство "value" = 321
[2024-10-04 20:16:43] Свойство "name"  = name 3
[2024-10-04 20:16:43] Свойство "value" = 333
```

[Наверх](#тип-array)

#### Функции для работы с типом ```array```

Для более удобной работы с типом ```array```, начиная с версии DaJet Script 3.9.2, реализованы ниже следующие функции. Кроме всего прочего эти функции можно использовать для буферизации отдельных сообщений ```object```, которые, например, являются частью потока данных команды **CONSUME**, чтобы организовать их пакетную обработку или доставку.

**Таблица функций типа ```array```**
|**Функция**|**Возврат**|**Параметры**|**Описание**|**Команда USE**|**Запрос СУБД**|**Выражение<br>DaJet Script**|
|---|---|---|---|---|---|---|
|ARRAY_COUNT|number|array|Функция возвращает количество элементов соответствующего ```array```.|нет|нет|да|
|ARRAY_CREATE|array|нет|Функция создаёт новый ```array```.|нет|нет|да|
|ARRAY_CREATE|array|number|Функция создаёт новый ```array``` с указанием начального размера внутреннего буфера.|нет|нет|да|
|ARRAY_APPEND|number|array<br>object|Функция добавляет ```object``` в самый конец ```array``` и возвращает индекс нового элемента.|нет|нет|да|
|ARRAY_SELECT|object|array<br>number|Функция возвращает ссылку на ```object``` по его индексу в ```array```.|нет|нет|да|
|ARRAY_DELETE|object|array<br>number|Функция удаляет элемент по его индексу в ```array``` и возвращает ссылку на удалённый ```object```.|нет|нет|да|
|ARRAY_INSERT|number|array<br>number<br>object|Функция вставляет ```object``` по указанному индексу в ```array``` и возвращает новое количество элементов в массиве. Если по указанному индексу уже существует элемент, то он и все элементы, следующие за ним, смещаются на один индекс в большую сторону (плюс один).|нет|нет|да|

**Пример использования функций типа ```array```**
```SQL
DECLARE @index number
DECLARE @count number
DECLARE @array array
DECLARE @object object

SET @index = 0
SET @array = ARRAY_CREATE()

SET @object = SELECT Индекс = @index, Имя = 'Code ' + @index
SET @index = ARRAY_APPEND(@array, @object) + 1

SET @object = SELECT Индекс = @index, Имя = 'Code ' + @index
SET @index = ARRAY_APPEND(@array, @object) + 1

SET @object = SELECT Индекс = @index, Имя = 'Code ' + @index
SET @index = ARRAY_APPEND(@array, @object) + 1

SET @index = 0
SET @count = ARRAY_COUNT(@array)

WHILE @index < @count
   SET @object = ARRAY_SELECT(@array, @index)
   PRINT JSON(@object)
   SET @index = @index + 1
END

PRINT 'Количество элементов массива: ' + @count

SET @object = ARRAY_DELETE(@array, 1)
SET @count = ARRAY_INSERT(@array, 0, @object)
PRINT 'Количество после DELETE и INSERT: ' + @count

SET @object = ARRAY_SELECT(@array, 0)
SET @index = ARRAY_APPEND(@array, @object)
PRINT 'Количество после SELECT и APPEND: ' + ARRAY_COUNT(@array)
PRINT 'Индекс последнего элемента: ' +  @index

FOR @object IN @array
   PRINT JSON(@object)
END
```

**Результат выполнения скрипта**
```
[2025-02-08 17:03:15] {"Индекс":0,"Имя":"Code 0"}
[2025-02-08 17:03:15] {"Индекс":1,"Имя":"Code 1"}
[2025-02-08 17:03:15] {"Индекс":2,"Имя":"Code 2"}
[2025-02-08 17:03:15] Количество элементов массива: 3
[2025-02-08 17:03:15] Количество после DELETE и INSERT: 3
[2025-02-08 17:03:15] Количество после SELECT и APPEND: 4
[2025-02-08 17:03:15] Индекс последнего элемента: 3
[2025-02-08 17:03:15] {"Индекс":1,"Имя":"Code 1"}
[2025-02-08 17:03:15] {"Индекс":0,"Имя":"Code 0"}
[2025-02-08 17:03:15] {"Индекс":2,"Имя":"Code 2"}
[2025-02-08 17:03:15] {"Индекс":1,"Имя":"Code 1"}
```

[Наверх](#тип-array)
