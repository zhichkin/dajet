## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Функция UUID1C

Функция **UUID1C** выполняет преобразование значений типа ```uuid```, полученных из базы данных, в формат 1С:Предприятие 8. Функция имеет следующие варианты использования (перегрузки):
- **UUID1C**(uuid)   -> uuid
- **UUID1C**(string) -> uuid
- **UUID1C**(binary) -> uuid
- **UUID1C**(entity) -> uuid

> **На заметку:** функция ```UUID1C``` имеет "зеркальный" аналог ```UUIDDB```.

**Пример использования функции UUID1C**
```SQL
DECLARE @object object

USE 'mssql://server/database'
   SELECT ОригинальнаяСсылка = Ссылка
        , ТипОбъекта = TYPEOF(Ссылка)
        , UUID_1C    = UUID1C(Ссылка)
     INTO @object
     FROM Справочник.Тестовый
    WHERE Код = '000000001'
END

MODIFY @object       -- Формируем новую ссылку в формате 1С
SELECT НоваяСсылка = ENTITY(@object.ТипОбъекта, @object.UUID_1C)

RETURN @object
```

**Результат выполнения скрипта**
|**ОригинальнаяСсылка**|**ТипОбъекта**|**UUID_1C**|**НоваяСсылка**|
|----------------------|--------------|-----------|---------------|
|{98:643c479d-cacf-4048-11f0-2426af738206}|98|af738206-2426-11f0-9d47-3c64cfca4840|{98:af738206-2426-11f0-9d47-3c64cfca4840}|

**Использование преобразованного значения в 1С**

![Использование uuid в 1С](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-database-convert-uuid-1c-code.png)

[Наверх](#функция-uuid1c)
