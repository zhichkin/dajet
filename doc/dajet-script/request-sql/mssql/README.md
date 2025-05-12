## DaJet Script

[Назад](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/request-sql/README.md)

### Произвольные запросы SQL Server

**Пример скрипта T-SQL для выполнения командой REQUEST**
```SQL
-- ********************
-- * Файл ms-test.sql *
-- ********************

IF OBJECT_ID('tempdb..#test_table') IS NOT NULL DROP TABLE #test_table;

CREATE TABLE #test_table(
   fBoolean  bit,
   fInteger  int,
   fDecimal  numeric(10,4),
   fDateTime datetime2,
   fString   nvarchar(128),
   fBinary   varbinary(max),
   fUuid     uniqueidentifier
);

INSERT #test_table
       (fBoolean, fInteger, fDecimal, fDateTime, fString, fBinary, fUuid)
VALUES (@Boolean, @Integer, @Decimal, @DateTime, @String, @Binary, @Uuid);

SELECT * FROM #test_table;

IF OBJECT_ID('tempdb..#test_table') IS NOT NULL DROP TABLE #test_table;
```

**Пример выполнения скрипта ```ms-test.sql``` командой REQUEST**
```SQL
-- ******************
-- * ms-request.djs *
-- ******************

DEFINE Запись(
  Булево        boolean,
  Целое         integer,
  Десятичное    decimal,
  ДатаВремя     datetime,
  Строка        string,
  Бинарник      binary,
  Идентификатор uuid
)
DECLARE @Таблица array OF Запись

REQUEST 'mssql://server/database?sql'
   WITH Script = 'file://code/sql/ms-test.sql'
 SELECT Boolean  = TRUE
      , Integer  = 123
      , Decimal  = 1.2345
      , DateTime = UTC(3)
      , String   = 'test тест'
      , Binary   = 0xdeadbeef
      , Uuid     = NEWUUID()
   INTO @Таблица

RETURN @Таблица
```

**Результат выполнения скрипта DaJet Script ```ms-request.djs```**
|Булево|Целое|Десятичное|ДатаВремя|Строка|Бинарник|Идентификатор|
|------|-----|----------|---------|------|--------|-------------|
|True|123|1.2345|12/05/2025 19:23:06|test тест|3q2+7w==|1653ce12-9b68-403d-8208-950eb1608a74|

[Наверх](#произвольные-запросы-sql-server)
