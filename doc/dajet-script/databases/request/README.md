## DaJet Script

[Реляционные базы данных](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/README.md)

### Команда REQUEST

DaJet Script реализует вызов хранимых процедур баз данных. Данный функционал реализован однообразно для MS SQL Server и PostgreSQL. Для PostgreSQL функционал доступен только начиная с 11-ой версии и выше.

Возврат значений из хранимых процедур возможен только в виде исходящих параметров. Это обусловлено соответствующими ограничениями реализации PostgreSQL. При этом на текущий момент времени DaJet Script поддерживает возврат только одного исходящего параметра. Входящих параметров может быть столько сколько позволяет соответствующая СУБД.

> Команда **REQUEST** не требует использования команды **USE** для создания соответствующего контекста базы данных. Вызов хранимой процедуры выполняется в собственном контексте, заданном строкой подключения.

#### Пример вызова хранимой процедуры MS SQL Server

```SQL
DECLARE @input  number = 1 -- Входящий параметр
DECLARE @output number = 1 -- Исходящий параметр

-- Строка подключения к базе данных дополняется именем
-- хранимой процедуры. В данном случае это test_db_proc.

REQUEST 'mssql://server/database/test_db_proc'
   WITH @input
   INTO @output

PRINT @output -- Фиксируем результат в файле dajet.log

RETURN @output -- Возвращаем результат вызывающей стороне
```

**Код соответствующей хранимой процедуры MS SQL Server**

```SQL
CREATE PROCEDURE [dbo].[test_db_proc]
	@input  int,
	@output int OUTPUT
AS
BEGIN
	SET NOCOUNT ON;

	SELECT @output = @input + 1;
END
```

[Наверх](#команда-request)

#### Пример вызова хранимой процедуры PostgreSQL

```SQL
DECLARE @input  number = 1 -- Входящий параметр
DECLARE @_output number = 1 -- Исходящий параметр

-- Строка подключения к базе данных дополняется именем
-- хранимой процедуры. В данном случае это test_db_proc.

REQUEST 'pgsql://postgres:postgres@localhost:5432/database/test_db_proc'
   WITH _input = @input
   INTO @_output

PRINT @_output -- Фиксируем результат в файле dajet.log

RETURN @_output -- Возвращаем результат вызывающей стороне
```

**Код соответствующей хранимой процедуры PostgreSQL**

```SQL
CREATE OR REPLACE PROCEDURE public.test_db_proc(
	      _input  numeric,
	INOUT _output numeric)
LANGUAGE 'plpgsql'
AS $BODY$
begin
	_output = _input + 1;
end;
$BODY$;
```

[Наверх](#команда-request)
