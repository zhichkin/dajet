## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Тип ```entity```

Ссылочные типы данных 1С:Предприятие 8 - это сущности, которые идентифицируются своим уникальным ключом (ссылкой), а не значениями своих свойств. При помощи такого ключа можно ссылаться на отдельно взятые сущности в свойствах других объектов. На уровне реляционной базы данных ссылки играют роль первичных и внешних ключей. Например, ссылочными типами данных являются справочники и документы 1С:Предприятие 8.

Тип данных ```entity``` - это ссылка. Внутренняя реализация ```entity``` представляет из себя структуру, которая состоит из двух полей: целочисленный код типа ссылки (integer) и уникальный идентификатор (UUID). Данная структура является указателем (ссылкой) на конкретный объект в базе данных или его отсутствие - "пустая ссылка".

> "Пустая ссылка" всегда строго типизированна, то есть указывает на отсутствие объекта какого-то определённого типа, например, "Справочник.Номенклатура".

> Специальным значением ```entity``` является "нулевая ссылка" (значение по умолчанию). Это ссылка, которая ни на что не указывает, так как ей пока что не присвоено конкретное значение.

**Представление различных видов ссылок ```entity```**
```
Нулевая    ссылка:  {0:00000000-0000-0000-0000-000000000000}
Пустая     ссылка: {36:00000000-0000-0000-0000-000000000000}
Конкретная ссылка: {36:08ec109d-a06b-a1b1-11ee-ca472bff0a0d}
```

Целочисленный код типа ссылки определяется и фиксируется в момент создания нового класса сущности в информационной базе 1С:Предприятие 8. В терминах 1С это называется "объект метаданных". Например, объект метаданных справочника "Номенклатура" может иметь код своего типа ```12``` в одной информационной базе. Однако, код типа одноимённого справочника в другой информационной базе 1С:Предприятие 8 может иметь другое значение, например, ```21```. Другими словами, код типа объекта метаданных зависит от контекста базы данных, в которой он был создан.

Уникальный идентификатор ссылки генерируется платформой 1С:Предприятие 8 или присваивается разработчиком прикладного решения в момент создания новой сущности определённого типа и её записи в базу данных. Эта возможность часто используется для синхронизации сущностей "по ссылке" при обменах данными между различными прикладными решениями.

Значение типа ```entity``` можно получить из базы данных 1С:Предприятие 8 при помощи следующего кода DaJet Script:

```TSQL
DECLARE @Ссылка entity

USE 'mssql://server/database'
   SELECT TOP 1 Ссылка INTO @Ссылка
     FROM Справочник.Номенклатура
END

PRINT @Ссылка

-- Результат выполнения скрипта
[2024-10-01 14:31:59] {36:08ec109d-a06b-a1b1-11ee-ca472bff0a0d}
```

В некоторых случаях требуется получить значение "пустой ссылки" определённого типа. Это можно сделать при помощи следующего кода DaJet Script. При этом важно отметить, что этот код будет работать корректно только "внутри" блока команды **USE**, так как получение метаданных и их кодов зависит от контекста соответствующей информационной базы 1С:Предприятие 8.

```TSQL
USE 'mssql://server/database'
   DECLARE @Ссылка Справочник.Номенклатура
   PRINT @Ссылка
END

-- Результат выполнения скрипта
[2024-10-01 14:46:58] {36:00000000-0000-0000-0000-000000000000}
```