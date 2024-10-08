## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Управление последовательностью

Управление последовательностью решает задачу согласованности данных в процессе регистрации их изменений и последующей доставки, обмена данными, между узлами интеграции по принципу FIFO. Механизм управления последовательностью основан на понятии "векторных часов". Платформа 1С:Предприятие 8 не имеет адекватных средств для решения этой задачи. Особенно это касается высоко нагруженных и конкурентных условий эксплуатации. Однако, на уровне СУБД этот механизм может быть реализован при помощи специальных объектов ядра - генераторов последовательностей (SEQUENCE).

Для управления последовательностью DaJet Script реализует набор команд и функций, которые позволяют работать с объектами SEQUENCE ядра СУБД: создавать и удалять, применять и отзывать для соответствующих объектов 1С:Предприятие 8, а также получать следующее значение последовательности в запросах.

> На уровне СУБД счётчик последовательности имеет тип данных ```bigint```.

- [CREATE SEQUENCE](#create-sequence)
- [DROP SEQUENCE](#drop-sequence)
- [APPLY SEQUENCE](#apply-sequence)
- [REVOKE SEQUENCE](#revoke-sequence)
- [Функция VECTOR](#функция-vector)
- [Дополнительные материалы](#дополнительные-материалы)

#### CREATE SEQUENCE

Команда для создания именованного объекта последовательности базы данных в случае его отсутствия.

```SQL
CREATE SEQUENCE <identifier>
```
**\<identifier\>** - имя объекта последовательности.

> Имя нового объекта последовательности указывается без одинарных кавычек.

**Создание последовательности ```so_my_sequence```**
```SQL
USE 'mssql://server/database'
   CREATE SEQUENCE so_my_sequence
END
```

**На уровне СУБД выполняется следующий код:**
```SQL
-- SQL Server
IF NOT EXISTS(SELECT 1 FROM sys.sequences WHERE name = 'so_my_sequence')
BEGIN
   CREATE SEQUENCE my_sequence AS bigint START WITH 1 INCREMENT BY 1;
END;

-- PostgreSQL
CREATE SEQUENCE IF NOT EXISTS so_my_sequence AS bigint INCREMENT BY 1 START WITH 1 CACHE 1;
```

[Наверх](#управление-последовательностью)

#### DROP SEQUENCE

Команда для удаления именованного объекта последовательности базы данных. В случае его отсутствия генерируется ошибка СУБД.

```SQL
DROP SEQUENCE <identifier>
```
**\<identifier\>** - имя объекта последовательности.

> Имя удаляемого объекта последовательности указывается без одинарных кавычек.

**Удаление последовательности ```so_my_sequence```**
```SQL
USE 'mssql://server/database'
   TRY
      DROP SEQUENCE so_my_sequence
   CATCH
      PRINT ERROR_MESSAGE()
   END -- TRY
END -- USE
```

**На уровне СУБД выполняется следующий код:**
```SQL
-- SQL Server
DROP SEQUENCE so_my_sequence;

-- PostgreSQL
DROP SEQUENCE so_my_sequence;
```

[Наверх](#управление-последовательностью)

#### APPLY SEQUENCE

Команда применяет ранее созданную последовательность к указанному измерению, например, регистра сведений, который используется в качестве очереди исходящих сообщений. Работа команды абсолютно прозрачна для 1С:Предприятие 8. Это означает, что код прикладного решения, выполняющего запись в регистр сведений, не меняется. Приращение значений соответствующего измерения будет осуществляться автоматически при добавлении новых записей в коде 1С:Предприятие 8.

> **Совет:** используйте для измерения регистра сведений тип данных ЧИСЛО(15,0).

Команда имеет необязательную опцию **RECALCULATE**. Эта опция позволяет выполнить пересчёт значений указанного измерения для существующих на данный момент в регистре сведений записей по порядку, начиная с текущего значения последовательности. Оригинальный порядок записей регистра при этом сохраняется таким же, какой он был до начала выполнения команды.

Рассмотрим использование команды **APPLY SEQUENCE** на практическом примере. Допустим, что у нас есть регистр сведений "ИсходящиеСообщения" для регистрации изменений и дальнейшей их передачи во внешние узлы интеграции, имеющий следующую структуру:
|**Реквизит**|**Назначение**|**Тип данных**|**Описание**|
|------------|--------------|--------------|------------|
|НомерСообщения|Измерение|ЧИСЛО(15,0)|Автоматически генерируемый СУБД последовательный номер сообщения|
|ТипСообщения|Ресурс|СТРОКА(1024)|Имя типа сообщения|
|ТелоСообщения|Ресурс|СТРОКА(0)|Тело сообщения в строковом формате, например, JSON|

Теперь выполним следующий код 1С:Предприятие 8:

![apply-sequence](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-apply-sequence-01.png)

В результате выполнения в пустом регистре будет создана одна запись и получена следующая ошибка:

![apply-sequence](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-apply-sequence-02.png)

![apply-sequence](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-apply-sequence-03.png)

Выполним следующий код DaJet Script:
```SQL
USE 'mssql://server/database'
   CREATE SEQUENCE so_my_sequence -- На всякий случай, если объект последовательности отсутствует
   TRY
      APPLY SEQUENCE so_my_sequence ON РегистрСведений.ИсходящиеСообщения(НомерСообщения)
   CATCH
      PRINT ERROR_MESSAGE()
   END
END
```

Заново выполним тот же самый код 1С:Предприятие 8  (на этот раз без ошибок), предварительно изменив уже записанное значение измерения "НомерСообщения" в регистре на любое другое отличное от нуля значение. Получим следующий результат:

![apply-sequence](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-apply-sequence-04.png)

**На уровне СУБД команда APPLY SEQUENCE выполняет следующий код:**
```SQL
-- SQL Server
IF OBJECT_ID('_inforg134_instead_of_insert', 'TR') IS NULL
EXECUTE('CREATE TRIGGER _inforg134_instead_of_insert ON _InfoRg134 INSTEAD OF INSERT NOT FOR REPLICATION AS
INSERT _InfoRg134(_Fld135, _Fld136, _Fld137)
SELECT NEXT VALUE FOR so_my_sequence, i._Fld136, i._Fld137
FROM INSERTED AS i;');

-- PostgreSQL
CREATE FUNCTION fn_inforg98_before_insert()
RETURNS trigger AS $BODY$
BEGIN
NEW._fld99 := nextval('so_my_sequence');
RETURN NEW;
END $BODY$ LANGUAGE 'plpgsql';

CREATE TRIGGER tr_inforg98_before_insert
BEFORE INSERT ON _inforg98 FOR EACH ROW
EXECUTE PROCEDURE fn_inforg98_before_insert();
```

**В случае использования опции RECALCULATE команды APPLY SEQUENCE дополнительно выполняется следующий код:**
```SQL
-- SQL Server
BEGIN TRANSACTION;
SELECT _Fld135, NEXT VALUE FOR so_my_sequence OVER (ORDER BY _Fld135 ASC) AS sequence_value
INTO #COPY_InfoRg134 FROM _InfoRg134 WITH (TABLOCKX, HOLDLOCK);
UPDATE T SET T._Fld135 = S.sequence_value FROM _InfoRg134 AS T
INNER JOIN #COPY_InfoRg134 AS S ON T._Fld135 = S._Fld135;
DROP TABLE #COPY_InfoRg134;
COMMIT TRANSACTION;

-- PostgreSQL
BEGIN TRANSACTION;
LOCK TABLE _inforg98 IN ACCESS EXCLUSIVE MODE;
WITH cte AS (SELECT _fld99, nextval('so_my_sequence') AS sequence_value
FROM _inforg98 ORDER BY _fld99 ASC)
UPDATE _inforg98 SET _fld99 = cte.sequence_value FROM cte
WHERE _inforg98._fld99 = cte._fld99;
COMMIT TRANSACTION;
```

[Наверх](#управление-последовательностью)

#### REVOKE SEQUENCE



[Наверх](#управление-последовательностью)

#### Функция VECTOR

Функция **VECTOR** возвращает следующее значение счётчика именованной последовательности. Гарантированно, что одно и тоже значение счётчика не может быть получено при повторном вызове этой функции, а также при одновременном вызове параллельно в разных потоках выполнения. Функция **VECTOR** всегда выполняется в контексте соответствующей базы данных, то есть требует использования "внутри" команды **USE**. Вызов функции имеет следующий синтаксис:
```SQL
VECTOR('<sequence_name>')
```
**\<sequence_name\>** - имя объекта последовательности. Имя заключено в одинарные кавычки - это строковой литерал, константа.

**1. Обращение к функции VECTOR вне контекста СУБД генерирует ошибку:**
```SQL
DECLARE @current number
SET @current = SELECT VECTOR('so_my_sequence')
-- Результат выполнения скрипта (ошибка):
-- Parent UseStatement is not found
```

**2. Указание несуществующей последовательности генерирует ошибку:**
```SQL
DECLARE @current number
USE 'mssql://server/database'
   SET @current = SELECT VECTOR('so_my_sequence')
END
-- Результат выполнения скрипта (ошибка):
-- SQL Server: Invalid object name 'so_my_sequence'
-- PostgreSQL: relation "so_my_sequence" does not exist
```

**3. Пример целевого использования функции VECTOR (без ошибок):**
```SQL
DECLARE @next    number
DECLARE @current number

USE 'mssql://server/database'

   -- 1. Вариант использования, @current = 0
   SET @current = SELECT VECTOR('so_my_sequence')

   -- 2. Вариант использования
   SELECT VECTOR('so_my_sequence') INTO @current

   -- Два вызова функции: @current = 2, @next = 0
   SET @next = @current + 1

   -- Фиксируем значения в логе программы
   PRINT 'Значения so_my_sequence:'
   PRINT '- текущее   = ' + @current
   PRINT '- следующее = ' + @next

   -- 3. Целевой вариант использования (запишем значение 2)
   INSERT РегистрСведений.ВходящиеСообщения
   SELECT НомерСообщения = @current
        , ТипСообщения   = 'тест'
        , ТелоСообщения  = 'test'

   -- 4. Целевой вариант использования (запишем значение 3)
   INSERT РегистрСведений.ВходящиеСообщения
   SELECT НомерСообщения = VECTOR('so_my_sequence')
        , ТипСообщения   = 'тест'
        , ТелоСообщения  = 'test'
END

-- Результат выполнения скрипта
[2024-10-07 21:46:30] Значения so_my_sequence:
[2024-10-07 21:46:30] - текущее   = 2
[2024-10-07 21:46:30] - следующее = 3
```

[Наверх](#управление-последовательностью)
