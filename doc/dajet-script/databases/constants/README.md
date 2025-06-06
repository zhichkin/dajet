## DaJet Script

[Реляционные базы данных](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/README.md)

### Константы

- [Простой тип данных](#простой-тип-данных)
- [Составной тип данных](#составной-тип-данных)

#### Простой тип данных

Следующий скрипт демонстрирует получение значения константы простого типа данных ```boolean```.

```SQL
DECLARE @value boolean -- Значение константы

USE 'mssql://server/database?mdex'

  SELECT Значение INTO @value FROM Константа.Булево

END

-- Вывод значения в интерфейсе DaJet Studio
RETURN 'Значение константы = ' + @value
```

[Наверх](#константы)

#### Составной тип данных

> **Внимание!** В данном примере значение константы сохраняется в свойстве ```Значение``` переменной типа ```object```. Этот способ получения значения константы следует использовать во всех случаях, когда константа имеет ссылочный или составной тип данных.

```SQL
DECLARE @object object -- Переменная для сохранения результата

USE 'mssql://server/database?mdex'
  SELECT Значение =
    CASE WHEN Константа.Значение IS Справочник.ОсновнойЗаимствованный THEN Заимствованный.Наименование
         WHEN Константа.Значение IS Справочник.РасширениеСобственный  THEN Собственный.Наименование
         ELSE 'Значение константы не установлено' END
    INTO @object
    FROM Константа.СоставнойТипСсылки AS Константа
    LEFT JOIN Справочник.ОсновнойЗаимствованный AS Заимствованный
      ON Константа.Значение = Заимствованный.Ссылка
    LEFT JOIN Справочник.РасширениеСобственный AS Собственный
      ON Константа.Значение = Собственный.Ссылка
END

-- Вывод результата в интерфейсе DaJet Studio
RETURN @object
```

**Результат выполнения скрипта**
|Значение|
|--------|
|Основной заимствованный 4|

[Наверх](#константы)