## DaJet Script

[Реляционные базы данных](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/README.md)

### Команда DELETE

DaJet Script реализует команду **DELETE** в самом простом её варианте.

```SQL
DECLARE @id number

USE 'mssql://server/database'

   SELECT MAX(НомерСообщения) INTO @id
     FROM РегистрСведений.ВходящиеСообщения

   DELETE РегистрСведений.ВходящиеСообщения
    WHERE НомерСообщения = @id

END
```

[Наверх](#команда-delete)
