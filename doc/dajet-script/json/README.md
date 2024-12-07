## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Функция JSON

Функция **JSON** выполняет преобразование объектных типов ```object``` и ```array``` в формат ```JSON``` туда и обратно.

**Таблица перегруженных вариантов функции**
|**Сигнатура**|**Параметр**|**Возврат**|
|-------------|------------|-----------|
|JSON(array) -> json|array|string|
|JSON(object) -> json|object|string|
|JSON(json) -> array|string|array|
|JSON(json) -> object|string|object|

**Пример использования функции**
```SQL
DECLARE @object object
DECLARE @json   string = '{
  "Булево": true,
  "Целое": 1234,
  "Десятичное": 12.34,
  "ДатаВремя": "2024-01-01T12:34:56",
  "Строка": "это строка текста",
  "Идентификатор": "08ec109d-a06b-a1b1-11ee-ca472bff0a0d"
}'

-- *********************************
-- * Преобразование json -> object *
-- *********************************
SET @object = JSON(@json)

DECLARE @count number = 0
DECLARE @index number = 0
-- NOTE: Определяем схему переменной @property типа object !!!
DECLARE @property object = SELECT Name = '', Type = '', Value = NULL

SET @count = PROPERTY_COUNT(@object)

WHILE @index < @count

   SET @property = GET_PROPERTY(@object, @index)

   IF PROPERTY_EXISTS(@object, @property.Name) THEN
      PRINT '[' + @property.Name + '] (' + @property.Type + ') {' + @property.Value + '}'
   END

   SET @index = @index + 1 -- take next property
END

-- *********************************
-- * Преобразование object -> json *
-- *********************************
PRINT JSON(@object)

-- Результат работы скрипта
[2024-10-04 16:38:44] [Булево] (boolean) {true}
[2024-10-04 16:38:44] [Целое] (number) {1234}
[2024-10-04 16:38:44] [Десятичное] (number) {12.34}
[2024-10-04 16:38:44] [ДатаВремя] (datetime) {2024-01-01 12:34:56}
[2024-10-04 16:38:44] [Строка] (string) {это строка текста}
[2024-10-04 16:38:44] [Идентификатор] (uuid) {08ec109d-a06b-a1b1-11ee-ca472bff0a0d}
[2024-10-04 16:38:44] {"Булево":true,"Целое":1234,"Десятичное":12.34,"ДатаВремя":"2024-01-01T12:34:56","Строка":"это строка текста","Идентификатор":"08ec109d-a06b-a1b1-11ee-ca472bff0a0d"}
```
[Наверх](#функция-json)
