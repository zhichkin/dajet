## DaJet Script

[Назад](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/request-sql/README.md)

### Произвольные запросы Sqlite

> **Внимание!** Если указанный файл базы данных Sqlite не существует, то он будет создан автоматически. Путь к базе данных указывается относительно корневого каталога установки DaJet.

**Пример скрипта Sqlite для выполнения командой REQUEST**
```SQL
-- ************************
-- * Файл sqlite-test.sql *
-- ************************

DROP TABLE IF EXISTS test_table;

CREATE TABLE IF NOT EXISTS test_table(
   fBoolean  integer,
   fInteger  integer,
   fDecimal  real,
   fDateTime text,
   fString   text,
   fBinary   blob,
   fUuid     blob
);

INSERT INTO test_table
       (fBoolean, fInteger, fDecimal, fDateTime, fString, fBinary, fUuid)
VALUES (@Boolean, @Integer, @Decimal, @DateTime, @String, @Binary, @Uuid);

SELECT * FROM test_table;

--DROP TABLE IF EXISTS test_table;
```

**Пример выполнения скрипта ```sqlite-test.sql``` командой REQUEST**
```SQL
-- **********************
-- * sqlite-request.djs *
-- **********************

DEFINE Запись(
  Булево        boolean,
  Целое         integer,
  Десятичное    decimal,
  ДатаВремя     datetime,
  Строка        string,
  Бинарник      binary,
  Идентификатор uuid
)
DECLARE @НаборЗаписей array OF Запись

REQUEST 'sqlite://test.db?sql'
   WITH Script = 'file://code/sql/sqlite-test.sql'
      , Transaction = 'Serializable'
 SELECT Boolean  = TRUE
      , Integer  = 123
      , Decimal  = 1.23
      , DateTime = NOW()
      , String   = 'test тест'
      , Binary   = 0xdeadbeef
      , Uuid     = NEWUUID()
   INTO @НаборЗаписей

RETURN @НаборЗаписей
```

**Результат выполнения скрипта DaJet Script ```sqlite-request.djs```**
|Булево|Целое|Десятичное|ДатаВремя|Строка|Бинарник|Идентификатор|
|------|-----|----------|---------|------|--------|-------------|
|True|123|1.23|12/05/2025 19:23:06|test тест|3q2+7w==|1653ce12-9b68-403d-8208-950eb1608a74|

[Наверх](#произвольные-запросы-sqlite)
