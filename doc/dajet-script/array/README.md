## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Тип ```array```

Тип данных ```array``` реализован DaJet Script в основном для работы с выборками из баз данных, а также для циклической и параллельной обработки данных при помощи команды **FOR**. Параллельная обработка данных рассмотрена в соответствующем разделе документации DaJet Script.

- [Создание ```array``` запросом к базе данных](#создание-array-запросом-к-базе-данных)
- [Создание ```array``` при помощи функции JSON](#создание-array-при-помощи-функции-json)

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
