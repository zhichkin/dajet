## DaJet Script

[Назад](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/request-sql/README.md)

### Произвольные запросы PostgreSQL

**Пример скрипта PL/SQL для выполнения командой REQUEST**
```SQL
-- ********************
-- * Файл pg-test.sql *
-- ********************

DROP TABLE IF EXISTS test_table;

CREATE TEMPORARY TABLE test_table(
   fBoolean  boolean,
   fInteger  integer,
   fDecimal  numeric(10,4),
   fDateTime timestamp without time zone,
   fString   varchar(128),
   fBinary   bytea,
   fUuid     uuid
);

INSERT INTO test_table
       (fBoolean, fInteger, fDecimal, fDateTime, fString, fBinary, fUuid)
VALUES (@Boolean, @Integer, @Decimal, @DateTime, @String, @Binary, @Uuid);

SELECT * FROM test_table;

DROP TABLE IF EXISTS test_table;
```

**Пример выполнения скрипта ```pg-test.sql``` командой REQUEST**
```SQL
-- ******************
-- * pg-request.djs *
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

REQUEST 'pgsql://postgres:postgres@localhost:5432/database?sql'
   WITH Script = 'file://code/sql/pg-test.sql'
 SELECT Boolean  = TRUE
      , Integer  = 123
      , Decimal  = 1.2345
      , DateTime = NOW(0)
      , String   = 'test тест'
      , Binary   = 0xdeadbeef
      , Uuid     = NEWUUID()
   INTO @Таблица

RETURN @Таблица
```

**Результат выполнения скрипта DaJet Script ```pg-request.djs```**
|Булево|Целое|Десятичное|ДатаВремя|Строка|Бинарник|Идентификатор|
|------|-----|----------|---------|------|--------|-------------|
|True|123|1.2345|12/05/2025 19:23:06|test тест|3q2+7w==|1653ce12-9b68-403d-8208-950eb1608a74|

[Наверх](#произвольные-запросы-postgresql)
